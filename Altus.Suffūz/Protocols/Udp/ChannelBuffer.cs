using Altus.Suffūz.Collections;
using Altus.Suffūz.Diagnostics;
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
    public class ChannelBuffer : IChannelBuffer<UdpMessage>
    {
        const int BUFFER_COMPACT_INTERVAL = 30000;

        public event MessageAvailableHandler<UdpMessage> MessageReceived;


        IPersistentDictionary<ushort, ulong> _sequenceNumbers;
        IPersistentDictionary<ulong, ushort> _segmentNumbers;
        IPersistentDictionary<ulong, SegmentList> _segments;

        //IDictionary<ushort, ulong> _sequenceNumbers;
        //IDictionary<ulong, ushort> _segmentNumbers;
        //IDictionary<ulong, SegmentList> _segments;

        IScheduler _scheduler;
        List<ExpirationTask> _tasks = new List<ExpirationTask>();

        public ChannelBuffer()
        {
        }

        public IChannel Channel
        {
            get;
            private set;
        }

        public bool IsInitialized
        {
            get;
            private set;
        }

        ulong _localMessageId = 0;
        public ulong LocalMessageId
        {
            get
            {
                return _localMessageId;
            }
        }

        public ulong IncrementLocalMessageId()
        {
            lock(Channel)
            {
                _localMessageId++;
                _sequenceNumbers[0] = _localMessageId;
                return _localMessageId;
            }
        }

        public virtual void Initialize(IChannel channel)
        {
            this.Channel = channel;

            var manager = App.Resolve<IManagePersistentCollections>();
            _scheduler = App.Resolve<IScheduler>();
            _scheduler.TaskExpired += OnTaskExpired;

            //App.Resolve<ISerializationContext>()
            //       .SetSerializer<UdpMessage, UdpMessageSerializer>(StandardFormats.BINARY);

            _sequenceNumbers = manager
                   .GetOrCreate<IPersistentDictionary<ushort, ulong>>(
                       Channel.Name + "_messageNumbers.bin",
                       (name) => new PersistentDictionary<ushort, ulong>(name, manager.GlobalHeap, true));
            _sequenceNumbers.Compact();

            _segmentNumbers = manager
               .GetOrCreate<IPersistentDictionary<ulong, ushort>>(
                   Channel.Name + "_segmentNumbers.bin",
                   (name) => new PersistentDictionary<ulong, ushort>(name, manager.GlobalHeap, true));
            _segmentNumbers.Compact();

            _segments = manager
              .GetOrCreate<IPersistentDictionary<ulong, SegmentList>>(
                  Channel.Name + "_segments.bin",
                  (name) => new PersistentDictionary<ulong, SegmentList>(name, manager.GlobalHeap, false));
            _segments.Compact();

            //_sequenceNumbers = new Dictionary<ushort, ulong>();
            //_segmentNumbers = new Dictionary<ulong, ushort>();
            //_segments = new Dictionary<ulong, SegmentList>();

            foreach (var seg in _segments.ToArray())
            {
                if (seg.Value.Segments.Count > 0)
                {
                    var timeout = seg.Value.Created.Add(seg.Value.Segments[0].TimeToLive);
                    if (timeout > CurrentTime.Now)
                    {
                        var task = _scheduler.Schedule(timeout,
                            (messageId) => { lock (Channel) { RemoveMessageTracking(messageId); } },
                            () => seg.Key);
                        _tasks.Add(new ExpirationTask(seg.Key, task));
                        continue;
                    }
                }
                _segments.Remove(seg.Key);
            }

            if (!_sequenceNumbers.TryGetValue(0, out _localMessageId))
            {
                _localMessageId = (ulong)((ulong)App.InstanceId << 48) + 1;
                // sets the local outbound sequence number - the inbound number will be set at the instance id.  
                // we can send to ourselves, so we need to track both separately
                _sequenceNumbers[0] = _localMessageId; 
            }

            // keep the buffers compacted
            _scheduler.Schedule(BUFFER_COMPACT_INTERVAL, () => Compact());

            IsInitialized = true;
        }

        protected IScheduler Scheduler
        {
            get { return _scheduler; }
        }

        protected virtual void OnTaskExpired(object sender, TaskExpiredEventArgs e)
        {
        }

        protected virtual void Compact()
        {
            lock (Channel)
            {
                _sequenceNumbers.Compact();
                _segmentNumbers.Compact();
                _segments.Compact();
            }
        }

        public virtual void AddInboundSegment(MessageSegment segment)
        {
            lock (Channel)
            {
                using (var scope = new FlushScope())
                {
                    UpdateSequenceNumber(segment.Sender, segment.SequenceNumber);
                    var segments = AddMessageSegment(segment);
                    UpdateMessageSegments(segment.MessageId, segments);
                    UpdateSegmentNumber(segment.MessageId, segment.SegmentNumber);

                    if (segments.Segments.Count == segment.SegmentCount)
                    {
                        UdpMessage udpMessage;

                        if (TryCreateMessage(segments, out udpMessage))
                        {
                            OnMessageReceived(udpMessage);
                        }
                        AfterMessageReceived(udpMessage);
                    }
                }
        }
    }

        protected virtual SegmentList AddMessageSegment(MessageSegment segment)
        {
            SegmentList segments;
            if (!_segments.TryGetValue(segment.MessageId, out segments))
            {
                segments = new SegmentList()
                {
                    Created = CurrentTime.Now
                };
                var task = _scheduler.Schedule(segments.Created.Add(segment.TimeToLive),
                    (messageId) => { lock (Channel) { RemoveMessageTracking(messageId); } },
                    () => segment.MessageId);
                _tasks.Add(new ExpirationTask(segment.MessageId, task));
            }

            if (!segments.Segments.Any(s => s.SegmentNumber == segment.SegmentNumber))
            {
                segments.Segments.Add(segment);
            }

            return segments;
        }

        protected virtual void UpdateMessageSegments(ulong messageId, SegmentList segments)
        {
            _segments[messageId] = segments;
        }

        protected virtual void UpdateSequenceNumber(ushort instanceId, ulong sequenceNumber)
        {
            _sequenceNumbers[instanceId] = sequenceNumber;
        }

        protected virtual void UpdateSegmentNumber(ulong messageId, ushort segmentNumber)
        {
            _segmentNumbers[messageId] = segmentNumber;
        }

        protected virtual void AfterMessageReceived(UdpMessage message)
        {
            if (!message.IsComplete)
            {
                ushort sender = 0;
                try
                {
                    sender = message.Sender;
                }
                catch { }

                Logger.LogWarn("Received invalid UDP message on channel {0} from sender {1}.", Channel.Name, sender);
            }

            RemoveMessageTracking(message.MessageId);
            
        }

        protected virtual void RemoveMessageTracking(ulong messageId)
        {
            _segments.Remove(messageId);
            _segmentNumbers.Remove(messageId);
            var task = _tasks.SingleOrDefault(t => t.MessageId == messageId);
            if (task != null)
            {
                task.Task.Cancel();
                _tasks.Remove(task);
            }
        }

        protected virtual bool TryCreateMessage(SegmentList segments, out UdpMessage message)
        {
            message = new UdpMessage(this.Channel, segments.Segments[0]);
            for (int i = 1; i < segments.Segments.Count; i++)
            {
                if (segments.Segments[i] is UdpHeader)
                {
                    message.UdpHeaderSegment = (UdpHeader)segments.Segments[i];
                }
                else
                {
                    message.AddSegment((UdpSegment)segments.Segments[i]);
                }
            }
            return message.IsComplete;
        }

        public virtual ulong RemoteMessageId(ushort instanceId)
        {
            lock(Channel)
            {
                return _sequenceNumbers[instanceId];
            }
        }

        public virtual void Reset()
        {
            lock(Channel)
            {
                if (!IsInitialized)
                    throw new InvalidOperationException("The channel buffer has not been initialized");
                _scheduler.TaskExpired -= OnTaskExpired;

                foreach(var item in _tasks)
                {
                    item.Task.Cancel();
                }
                _tasks.Clear();

                _sequenceNumbers.Clear(true);
                _sequenceNumbers.Dispose();
                _segments.Clear(true);
                _segments.Dispose();
                _segmentNumbers.Clear(true);
                _segmentNumbers.Dispose();
                //_sequenceNumbers.Clear();
                //_segments.Clear();
                //_segmentNumbers.Clear();

                Initialize(this.Channel);
            }
        }

        protected virtual void OnMessageReceived(UdpMessage message)
        {
            if (this.MessageReceived != null)
            {
                this.MessageReceived(this.Channel, new MessageAvailableEventArgs<UdpMessage>(message));
            }
        }

        public class SegmentList
        {
            public SegmentList()
            {
                Segments = new List<MessageSegment>();
            }
            [BinarySerializable(0)]
            public DateTime Created { get; set; }
            [BinarySerializable(1)]
            public List<MessageSegment> Segments { get; set; }
        }

        public class ExpirationTask
        {
            public ExpirationTask(ulong messageId, IScheduledTask task)
            {
                MessageId = messageId;
                Task = task;
            }

            public ulong MessageId { get; private set; }
            public IScheduledTask Task { get; private set; }
        }
    }
}
