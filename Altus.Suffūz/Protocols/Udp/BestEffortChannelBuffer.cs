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
        IPersistentDictionary<ulong, NAKSegment> _nakBuffer;

        public event MissedSegmentsHandler MissedSegments;
        public event ResendSegmentHandler ResendSegment;

        public BestEffortChannelBuffer() : base()
        {
        }

        public override void Initialize(IChannel channel)
        {
            if (!IsInitialized)
            {
                var manager = App.Resolve<IManagePersistentCollections>();

                _nakBuffer = manager
                    .GetOrCreate<IPersistentDictionary<ulong, NAKSegment>>(
                        channel.Name + "_nak.bin",
                        (name) => new PersistentDictionary<ulong, NAKSegment> (name, manager.GlobalHeap, false));
                _nakBuffer.Compact();

                var now = CurrentTime.Now;
                foreach (var nak in _nakBuffer.ToArray())
                {
                    var timeout = nak.Value.Created.Add(nak.Value.Segment.TimeToLive);
                    if (timeout > CurrentTime.Now)
                    {
                        var task = this.Scheduler.Schedule(timeout,
                            (segmentId) => { RemoveRetrySegment(segmentId);  },
                            () => nak.Key);
                        this.Tasks.Add(new ExpirationTask(nak.Key, task));
                        continue;
                    }
                    _nakBuffer.Remove(nak.Key);
                }

                base.Initialize(channel);
            }
        }

        protected override void Compact()
        {
            _syncLock.Lock(() =>
            {
                _nakBuffer.Compact();
                base.Compact();
            });
        }

        public override void Reset()
        {
            _syncLock.Lock(() =>
            {
                if (!IsInitialized)
                    throw new InvalidOperationException("The channel buffer has not been initialized");

                _nakBuffer.Clear(true);
                _nakBuffer.Dispose();

                _sortedSegments.Clear();

                base.Reset();
            });
        }

        public void AddRetrySegment(MessageSegment segment)
        {
            _syncLock.Lock(() =>
            {
                var now = CurrentTime.Now;
                if (segment.TimeToLive.TotalMilliseconds > 0)
                {
                    // add message to nak buffer
                    _nakBuffer.Add(segment.SegmentId, new NAKSegment() { Created = now, Segment = segment });

                    // set the message to expire from the nak buffer based on the message's TTL
                    var task = this.Scheduler.Schedule(now.Add(segment.TimeToLive),
                           (segmentId) => RemoveRetrySegment(segmentId),
                           () => segment.SegmentId);
                    this.Tasks.Add(new ExpirationTask(segment.SegmentId, task));
                }
            });
        }

        public void RemoveRetrySegment(MessageSegment message)
        {
            RemoveRetrySegment(message.SegmentId);
        }

        public void RemoveRetrySegment(ulong segmentId)
        {
            _syncLock.Lock(() =>
            {
                _nakBuffer.Remove(segmentId);
                var task = Tasks.SingleOrDefault(t => t.Task == Scheduler.CurrentTask);
                if (task != null)
                {
                    task.Task.Cancel();
                    Tasks.Remove(task);
                }
            });
        }

        public MessageSegment GetRetrySegement(ulong segmentId)
        {
            return _syncLock.Lock(() =>
            {
                try
                {
                    return _nakBuffer[segmentId].Segment;
                }
                catch (KeyNotFoundException)
                {
                    return null;
                }
            });
        }

        Dictionary<ushort, List<MessageSegment>> _sortedSegments = new Dictionary<ushort, List<MessageSegment>>();
        public override void AddInboundSegment(MessageSegment segment)
        {
            _syncLock.Lock(() =>
            {
                if (!(segment is UdpSegmentNAK))
                {
                    // add the new segment in sorted order to the existing segment list
                    AddSortedSegment(segment);
                    // request any missing segments
                    RequestMissingSegments(segment.Sender);
                }
                // in any case, allow the packet to be handled
                base.AddInboundSegment(segment);
            });
        }

        protected override void RemoveInboundMessage(ulong messageId)
        {
            _syncLock.Lock(() =>
            {
                ushort sender;
                ulong seqNo;
                UdpMessage.SplitMessageId(messageId, out sender, out seqNo);

                List<MessageSegment> segments;
                if (_sortedSegments.TryGetValue(sender, out segments))
                {
                    foreach(var segment in segments.ToArray())
                    {
                        if (segment.MessageId == messageId)
                        {
                            segments.Remove(segment);
                        }
                    }
                }

                base.RemoveInboundMessage(messageId);
            });
        }

        protected override bool TryCreateMessage(SegmentList segments, out UdpMessage message)
        {
            if (segments.Segments[0] is UdpSegmentNAK)
            {
                var segment = segments.Segments[0] as UdpSegmentNAK;
                if (segment.Sender == App.InstanceId)
                {
                    // it's one of ours
                    SendMissingSegments((UdpSegmentNAK)segments.Segments[0]);
                }
                RemoveInboundMessage(segment.MessageId);
                message = null;
                return false;
            }
            else
            {
                return base.TryCreateMessage(segments, out message);
            }
        }

        protected virtual void SendMissingSegments(UdpSegmentNAK udpSegmentNAK)
        {
            _syncLock.Lock(() => 
            {
                for(ulong i = udpSegmentNAK.SegmentStart; i < udpSegmentNAK.SegmentEnd; i++)
                {
                    NAKSegment nakSegment;
                    if (_nakBuffer.TryGetValue(i, out nakSegment))
                    {
                        OnResendSegment(nakSegment.Segment);
                    }
                }
            });
        }

        protected virtual void OnResendSegment(MessageSegment segment)
        {
            if (ResendSegment != null)
            {
                ResendSegment(this, new ResendSegmentEventArgs(segment));
            }
        }

        protected virtual void RequestMissingSegments(ushort sender)
        {
            // this will aggressively request missing packets whenever a new packet arrives
            // in the event of several missing packets, this might end up requesting the same packets being resent multiple times
            // as the range of missed packets is reduced when new packets arrive.  The only way to avoid this would be to 
            // move the NAK/retry process onto a more complex and separate background process that uses its own timing loop
            // to request missing items, rather than using the arrival of new packets as the trigger to check for missed items
            _syncLock.Lock(() =>
            {
                MessageSegment segment = null, lastSegment = null;
                List<MessageSegment> segments = _sortedSegments[sender];
                for (int i = 0; i < segments.Count; i++)
                {
                    segment = segments[i];
                    if (i == 0 && segment.SegmentNumber > 1)
                    {
                        // we need to check that we didn't pick up the first segment mid-message
                        // if so, we can ask for the missing start packets
                        OnMissedSegments(segment.Sender, segment.SegmentId - segment.SegmentNumber + 1, segment.SegmentId, segment.TimeToLive);
                    }
                    if (i > 0)
                    {
                        // after the first segment, just look for breaks in segment sequence and request to fill the gaps
                        if (segment.SegmentId - lastSegment.SegmentId > 1)
                        {
                            // we skipped some packets, ask for them
                            OnMissedSegments(segment.Sender, lastSegment.SegmentId + 1, segment.SegmentId, segment.TimeToLive);
                        }
                    }
                    lastSegment = segment;
                }
            });
        }

        protected virtual void AddSortedSegment(MessageSegment segment)
        {
            _syncLock.Lock(() =>
            {
                List<MessageSegment> segments;
                if (!_sortedSegments.TryGetValue(segment.Sender, out segments))
                {
                    segments = new List<MessageSegment>();
                    _sortedSegments.Add(segment.Sender, segments);
                }

                int index = segments.Count;
                bool exists = false;
                for (int i = segments.Count; i > 0; i--)
                {
                    index = i - 1;
                    // assume new segments will usually be at the bottom of the list, 
                    // so we go backwards from bottom to top
                    if (segment.SegmentId > segments[i - 1].SegmentId)
                    {
                        index++;
                        break;
                    }
                    else if (segment.SegmentId == segments[i - 1].SegmentId)
                    {
                        // we already have it, so ignore it
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    segments.Insert(index, segment);
                    var task = Scheduler.Schedule(CurrentTime.Now.Add(segment.TimeToLive),
                        (seg) => RemoveSortedSegment(seg),
                        () => segment);
                }
            });
        }

        protected virtual void RemoveSortedSegment(MessageSegment segment)
        {
            _syncLock.Lock(() =>
            {
                var segments = _sortedSegments[segment.Sender];
                segments.Remove(segment);
            });
        }

        Dictionary<ushort, List<MissedSegmentsEventArgs>> _pendingNAKs = new Dictionary<ushort, List<MissedSegmentsEventArgs>>();
        protected virtual void OnMissedSegments(ushort senderId, ulong startSegment, ulong endSegment, TimeSpan timeToLive)
        {
            _syncLock.Lock(() => 
            {
                List<MissedSegmentsEventArgs> missed;
                if (!_pendingNAKs.TryGetValue(senderId, out missed))
                {
                    missed = new List<MissedSegmentsEventArgs>();
                    _pendingNAKs.Add(senderId, missed);
                }
                // guard against a flury of requests for the same range already in flight
                if (!missed.Any(m => m.StartSegmentId <= startSegment && m.EndSegmentId >= endSegment))
                {
                    if (MissedSegments != null)
                    {
                        var e = new MissedSegmentsEventArgs(senderId, App.InstanceId, startSegment, endSegment);
                        missed.Add(e);
                        MissedSegments(this, e);
                        // allow 50 ms per segment requested, and then expire the item, allowing requests to be sent again
                        var task = Scheduler.Schedule(CurrentTime.Now.Add(TimeSpan.FromMilliseconds(50 * (endSegment - startSegment))),
                            (me) => RemoveMissedSegment(me),
                            () => e);
                    }
                }
            });
        }

        protected virtual void RemoveMissedSegment(MissedSegmentsEventArgs me)
        {
            _syncLock.Lock(() =>
            {
                List<MissedSegmentsEventArgs> missed;
                if (_pendingNAKs.TryGetValue(me.SenderId, out missed))
                {
                    missed.Remove(me);
                }
            });
        }
    }

    public class NAKSegment
    {
        [BinarySerializable(0)]
        public DateTime Created { get; set; }
        [BinarySerializable(1)]
        public MessageSegment Segment { get; set; }
    }
}
