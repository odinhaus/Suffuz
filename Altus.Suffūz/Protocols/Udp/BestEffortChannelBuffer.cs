using Altus.Suffūz.Collections;
using Altus.Suffūz.Scheduling;
using Altus.Suffūz.Serialization;
using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols.Udp
{
    public class BestEffortChannelBuffer : ChannelBuffer, IBestEffortChannelBuffer<UdpMessage>
    {
        IPersistentDictionary<ulong, NAKMessage> _nakBuffer;
        IPersistentList<UdpSegmentNAK> _pendingNAKs;

        List<ExpirationTask> _tasks = new List<ExpirationTask>();

        public event MissedSegmentsHandler MissedSegments;

        public BestEffortChannelBuffer() : base()
        {
        }

        public override void Initialize(IChannel channel)
        {
            if (!IsInitialized)
            {
                var manager = App.Resolve<IManagePersistentCollections>();

                _nakBuffer = manager
                    .GetOrCreate<IPersistentDictionary<ulong, NAKMessage>>(
                        Channel.Name + "_nak.bin",
                        (name) => new PersistentDictionary<ulong, NAKMessage> (name, manager.GlobalHeap, false));
                _nakBuffer.Compact();

                _pendingNAKs = manager
                    .GetOrCreate<IPersistentList<UdpSegmentNAK>>(
                        Channel.Name + "_pendingNAKs.bin",
                        (name) => new PersistentList<UdpSegmentNAK>(name, 1024 * 1024) { AutoGrowSize = 1024 * 1024 });
                _pendingNAKs.Compact();

                var now = CurrentTime.Now;
                foreach (var nak in _nakBuffer.ToArray())
                {
                    var timeout = nak.Value.Created.Add(nak.Value.Segment.TimeToLive);
                    if (timeout > CurrentTime.Now)
                    {
                        var task = this.Scheduler.Schedule(timeout,
                            (segmentId) => { lock (Channel) { RemoveRetrySegment(segmentId); } },
                            () => nak.Key);
                        this.Tasks.Add(new ExpirationTask(nak.Key, task));
                        continue;
                    }
                    _nakBuffer.Remove(nak.Key);
                }

                base.Initialize(this.Channel);
            }
        }

        protected override void Compact()
        {
            lock(Channel)
            {
                _nakBuffer.Compact();
            }
            base.Compact();
        }

        public override void Reset()
        {
            lock(Channel)
            {
                if (!IsInitialized)
                    throw new InvalidOperationException("The channel buffer has not been initialized");

                _nakBuffer.Clear(true);
                _nakBuffer.Dispose();

                base.Reset();
            }
        }

        public void AddRetrySegment(MessageSegment segment)
        {
            lock(Channel)
            {
                var now = CurrentTime.Now;
                if (segment.TimeToLive.TotalMilliseconds > 0)
                {
                    // add message to nak buffer
                    _nakBuffer.Add(segment.MessageId, new NAKMessage() { Created = now, Segment = segment });

                    // set the message to expire from the nak buffer based on the message's TTL
                    var task = this.Scheduler.Schedule(now.Add(segment.TimeToLive),
                           (segmentId) => { lock (Channel) { RemoveRetrySegment(segmentId); } },
                           () => segment.SegmentId);
                    this.Tasks.Add(new ExpirationTask(segment.SegmentId, task));
                }
            }
        }

        public void RemoveRetrySegment(MessageSegment message)
        {
            RemoveRetrySegment(message.SegmentId);
        }

        public void RemoveRetrySegment(ulong segmentId)
        {
            lock (Channel)
            {
                _nakBuffer.Remove(segmentId);
                var task = Tasks.SingleOrDefault(t => t.MessageId == segmentId);
                if (task != null)
                {
                    task.Task.Cancel();
                    Tasks.Remove(task);
                }
            }
        }

        public MessageSegment GetRetrySegement(ulong segmentId)
        {
            lock (Channel)
            {
                try
                {
                    return _nakBuffer[segmentId].Segment;
                }
                catch(KeyNotFoundException)
                {
                    return null;
                }
            }
        }

        public override void AddInboundSegment(MessageSegment segment)
        {
            lock(Channel)
            {
                ulong lastSegmentId;
                if (SegmentIds.TryGetValue(segment.Sender, out lastSegmentId)
                    && segment.SegmentId > lastSegmentId + 1)
                {
                    // this packet has jumped forward in time - we missed some packets, tell the channel
                    OnMissedSegments(segment.Sender, lastSegmentId + 1, segment.SegmentId);
                }
                // in any case, allow the packet to be handled
                base.AddInboundSegment(segment);
            }
        }

        protected virtual void OnMissedSegments(ushort senderId, ulong startSegment, ulong endSegment)
        {
            if (MissedSegments != null)
            {
                MissedSegments(this, new MissedSegmentsEventArgs(senderId, App.InstanceId, startSegment, endSegment));
            }
        }


        public virtual void AddSegmentNAK(UdpSegmentNAK nak)
        {
            _pendingNAKs.Add(nak);
            var task = this.Scheduler.Schedule(CurrentTime.Now.Add(TimeSpan.FromSeconds(nak.SegmentEnd - nak.SegmentStart + 1)),
                           (n) => { lock (Channel) { RemoveSegmentNAK(n); } },
                           () => nak);
            this.Tasks.Add(new ExpirationTask(nak.SegmentEnd, task));
        }

        public virtual bool RemoveSegmentNAK(UdpSegmentNAK nak)
        {
            var found = _pendingNAKs.Remove(nak);
            return found;
        }

        public int RetryCount { get { return _nakBuffer.Count; } }

        protected List<ExpirationTask> Tasks { get { return _tasks; } }

    }

    public class NAKMessage
    {
        [BinarySerializable(0)]
        public DateTime Created { get; set; }
        [BinarySerializable(1)]
        public MessageSegment Segment { get; set; }
    }
}
