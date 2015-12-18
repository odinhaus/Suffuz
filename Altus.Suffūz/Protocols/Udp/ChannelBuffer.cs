using Altus.Suffūz.Collections;
using Altus.Suffūz.Diagnostics;
using Altus.Suffūz.Scheduling;
using Altus.Suffūz.Serialization;
using Altus.Suffūz.Serialization.Binary;
using Altus.Suffūz.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Altus.Suffūz.Protocols.Udp
{
    public class ChannelBuffer : IChannelBuffer<UdpMessage>
    {
        const int BUFFER_COMPACT_INTERVAL = 30000;
        const int BUFFER_COMMIT_INTERVAL = 10000;

        public event MessageAvailableHandler<UdpMessage> MessageReceived;


        IPersistentDictionary<ushort, ulong> _messageIds;
        IPersistentDictionary<ushort, ulong> _segmentIds;
        //IPersistentDictionary<ulong, ushort> _segmentNumbers;
        //IPersistentDictionary<ulong, SegmentList> _segments;

        protected ExclusiveLock _syncLock;
        
        IDictionary<ulong, ushort> _segmentNumbers;
        IDictionary<ulong, SegmentList> _segments;

        IScheduler _scheduler;
        Dictionary<Guid, ExpirationTask> _tasks = new Dictionary<Guid, ExpirationTask>();

        public ChannelBuffer()
        {
            _scheduler = App.Resolve<IScheduler>();
            _scheduler.TaskExpired += OnTaskExpired;
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
        public ExclusiveLock SyncLock { get { return _syncLock; } }
        protected IScheduler Scheduler { get { return _scheduler; } }
        protected IDictionary<ulong, ushort> SegmentNumbers { get { return _segmentNumbers; } }
        protected IDictionary<ulong, SegmentList> Segments { get { return _segments; } }
        protected IPersistentDictionary<ushort, ulong> SegmentIds {  get { return _segmentIds; } }
        protected Dictionary<Guid, ExpirationTask> Tasks {  get { return _tasks; } }

        ulong _localMessageId = 0;
        public ulong LocalMessageId
        {
            get
            {
                return _localMessageId;
            }
        }

        public ulong LocalSegmentId
        {
            get
            {
                return SyncLock.Lock(() =>
                {
                    var id = _segmentIds[0];
                    id++;
                    _segmentIds[0] = id;
                    return id;
                });
            }
        }

        public ulong IncrementLocalMessageId()
        {
            return SyncLock.Lock(() =>
            {
                _localMessageId++;
                _messageIds[0] = _localMessageId;
                return _localMessageId;
            });
        }

        public virtual void Initialize(IChannel channel)
        {
            this.Channel = channel;

            var manager = App.Resolve<IManagePersistentCollections>();

            _messageIds = manager
                   .GetOrCreate<IPersistentDictionary<ushort, ulong>>(
                       Channel.Name + "_messageIds.bin",
                       (name) => new PersistentDictionary<ushort, ulong>(name, manager.GlobalHeap, true));

            _segmentIds = manager
                   .GetOrCreate<IPersistentDictionary<ushort, ulong>>(
                       Channel.Name + "_segmentIds.bin",
                       (name) => new PersistentDictionary<ushort, ulong>(name, manager.GlobalHeap, true));

            _syncLock = _messageIds.SyncLock;
           
            _segmentNumbers = new Dictionary<ulong, ushort>();
            _segments = new Dictionary<ulong, SegmentList>();

            foreach (var seg in _segments.ToArray())
            {
                if (seg.Value.Segments.Count > 0)
                {
                    var timeout = seg.Value.Created.Add(seg.Value.Segments[0].TimeToLive);
                    if (timeout > CurrentTime.Now)
                    {
                        var task = _scheduler.Schedule(timeout,
                            (messageId) => { RemoveInboundMessage(messageId); },
                            () => seg.Key);
                        Tasks.Add(task.Id, new ExpirationTask(seg.Key, task));
                        continue;
                    }
                }
                _segments.Remove(seg.Key);
            }

            if (!_messageIds.TryGetValue(0, out _localMessageId))
            {
                _localMessageId = (ulong)((ulong)App.InstanceId << 48) + 1;
                // sets the local outbound sequence number - the inbound number will be set at the instance id.  
                // we can send to ourselves, so we need to track both separately
                _messageIds[0] = _localMessageId; 
            }

            ulong segmentId;
            if (!_segmentIds.TryGetValue(0, out segmentId))
            {
                segmentId = 1;
                _segmentIds[0] = segmentId;
            }

            IsInitialized = true;
        }

        public TransactionScope Transaction { get; private set; }

        protected virtual void OnTaskExpired(object sender, TaskExpiredEventArgs e)
        {
        }

        public virtual void AddInboundSegment(MessageSegment segment)
        {
            SyncLock.Lock(() =>
            {
                using (var scope = new FlushScope())
                {
                    UpdateMessageId(segment.Sender, segment.SegmentId);
                    var segments = AddMessageSegment(segment);
                    UpdateMessageSegments(segment.MessageId, segments);
                    UpdateSegmentNumber(segment.MessageId, segment.SegmentNumber);

                    if (segments.Segments.Count == segment.SegmentCount)
                    {
                        UdpMessage udpMessage;

                        if (TryCreateMessage(segments, out udpMessage))
                        {
                            OnMessageReceived(udpMessage);
                            AfterMessageReceived(udpMessage.MessageId);
                        }
                    }
                }
            });
        }

        protected virtual SegmentList AddMessageSegment(MessageSegment segment)
        {
            SegmentList segments = null;
            SyncLock.Lock(() =>
            {
                if (!_segments.TryGetValue(segment.MessageId, out segments))
                {
                    segments = new SegmentList()
                    {
                        Created = CurrentTime.Now
                    };

                    if (segment.TimeToLive.TotalMilliseconds > 0)
                    {
                        var task = _scheduler.Schedule(segments.Created.Add(segment.TimeToLive),
                            (messageId) => { RemoveInboundMessage(messageId); },
                            () => segment.MessageId);
                        Tasks.Add(task.Id, new ExpirationTask(segment.MessageId, task));
                    }
                }
            });

            if (!segments.Segments.Any(s => s.SegmentNumber == segment.SegmentNumber))
            {
                segments.Segments.Add(segment);
            }

            return segments;
        }

        protected virtual void UpdateMessageSegments(ulong messageId, SegmentList segments)
        {
            SyncLock.Lock(() =>
            {
                _segments[messageId] = segments;
            });
        }

        protected virtual void UpdateMessageId(ushort instanceId, ulong messageId)
        {
            if (instanceId != 0) return; // we only store our own messageId

            SyncLock.Lock(() =>
            {
                _messageIds[instanceId] = messageId;
            });
        }

        protected virtual void UpdateSegmentNumber(ulong messageId, ushort segmentNumber)
        {
            SyncLock.Lock(() =>
            {
                _segmentNumbers[messageId] = segmentNumber;
            });
        }

        protected virtual void AfterMessageReceived(ulong messageId)
        {
            RemoveInboundMessage(messageId);
        }

        protected virtual void RemoveInboundMessage(ulong messageId)
        {
            SyncLock.Lock(() =>
            {
                _segments.Remove(messageId);
                _segmentNumbers.Remove(messageId);

                Tasks.Remove(Scheduler.CurrentTask?.Id ?? Guid.Empty);
            });
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

        public virtual void Reset()
        {
            SyncLock.Lock(() =>
            {
                if (!IsInitialized)
                    throw new InvalidOperationException("The channel buffer has not been initialized");
                _scheduler.TaskExpired -= OnTaskExpired;

                foreach (var item in Tasks.Values)
                {
                    item.Task.Cancel();
                }
                Tasks.Clear();

                _messageIds.Clear(true);
                _messageIds.Dispose();

                _segmentIds.Clear(true);
                _segmentIds.Dispose();

                //_segments.Clear(true);
                //_segments.Dispose();
                //_segmentNumbers.Clear(true);
                //_segmentNumbers.Dispose();
                //_sequenceNumbers.Clear();
                _segments.Clear();
                _segmentNumbers.Clear();
                IsInitialized = false;
                Initialize(this.Channel);
            });
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
