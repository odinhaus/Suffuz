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

        ExclusiveLock _syncLock;

        //IDictionary<ushort, ulong> _sequenceNumbers;
        IDictionary<ulong, ushort> _segmentNumbers;
        IDictionary<ulong, SegmentList> _segments;

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

        protected List<ExpirationTask> InboundTasks { get { return _tasks; } }
        protected IScheduler Scheduler { get { return _scheduler; } }
        //protected IPersistentDictionary<ushort, ulong> MessageIds { get { return _messageIds; } }
        protected IDictionary<ulong, ushort> SegmentNumbers { get { return _segmentNumbers; } }
        protected IDictionary<ulong, SegmentList> Segments { get { return _segments; } }
        protected IPersistentDictionary<ushort, ulong> SegmentIds {  get { return _segmentIds; } }

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
                return _syncLock.Lock(() =>
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
            return _syncLock.Lock(() =>
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
            _scheduler = App.Resolve<IScheduler>();
            _scheduler.TaskExpired += OnTaskExpired;

            //App.Resolve<ISerializationContext>()
            //       .SetSerializer<UdpMessage, UdpMessageSerializer>(StandardFormats.BINARY);

            _messageIds = manager
                   .GetOrCreate<IPersistentDictionary<ushort, ulong>>(
                       Channel.Name + "_messageIds.bin",
                       (name) => new PersistentDictionary<ushort, ulong>(name, manager.GlobalHeap, true));
            _messageIds.Compact();

            _segmentIds = manager
                   .GetOrCreate<IPersistentDictionary<ushort, ulong>>(
                       Channel.Name + "_segmentIds.bin",
                       (name) => new PersistentDictionary<ushort, ulong>(name, manager.GlobalHeap, true));
            _segmentIds.Compact();

            //_segmentNumbers = manager
            //   .GetOrCreate<IPersistentDictionary<ulong, ushort>>(
            //       Channel.Name + "_segmentNumbers.bin",
            //       (name) => new PersistentDictionary<ulong, ushort>(name, manager.GlobalHeap, true));
            //_segmentNumbers.Compact();

            //_segments = manager
            //  .GetOrCreate<IPersistentDictionary<ulong, SegmentList>>(
            //      Channel.Name + "_segments.bin",
            //      (name) => new PersistentDictionary<ulong, SegmentList>(name, manager.GlobalHeap, false));
            //_segments.Compact();

            _syncLock = _messageIds.SyncLock;

            //_sequenceNumbers = new Dictionary<ushort, ulong>();
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
                        _tasks.Add(new ExpirationTask(seg.Key, task));
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

            // keep the buffers compacted
            _scheduler.Schedule(BUFFER_COMPACT_INTERVAL, () => Compact());
            //Transaction = new TransactionScope();
            //_scheduler.Schedule(BUFFER_COMMIT_INTERVAL, () =>
            //{
            //    _syncLock.Lock(() =>
            //    {
            //        Transaction.Complete();
            //        Transaction.Dispose();
            //        Transaction = new TransactionScope();
            //    });
            //});

            IsInitialized = true;
        }

        public TransactionScope Transaction { get; private set; }

        protected virtual void OnTaskExpired(object sender, TaskExpiredEventArgs e)
        {
        }

        protected virtual void Compact()
        {
            _syncLock.Lock(() =>
            {
                _messageIds.Compact();
                _segmentIds.Compact();
                //_segmentNumbers.Compact();
                //_segments.Compact();
            });
        }

        public virtual void AddInboundSegment(MessageSegment segment)
        {
            _syncLock.Lock(() =>
            {
                using (var scope = new FlushScope())
                {
                    UpdateMessageId(segment.Sender, segment.SegmentId);
                    var segments = AddMessageSegment(segment);
                    UpdateMessageSegments(segment.MessageId, segments);
                    UpdateSegmentNumber(segment.MessageId, segment.SegmentNumber);
                    UpdateSegmentId(segment);


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
            });
        }

        protected virtual void UpdateSegmentId(MessageSegment segment)
        {
            _syncLock.Lock(() =>
            {
                if (!_segmentIds.ContainsKey(segment.Sender) || segment.SegmentId > _segmentIds[segment.Sender])
                {
                    // only update the segment if it's greater than our greatest segment id for this sender
                    _segmentIds[segment.Sender] = segment.SegmentId;
                }
            });
        }

        protected virtual SegmentList AddMessageSegment(MessageSegment segment)
        {
            SegmentList segments = null;
            _syncLock.Lock(() =>
            {
                if (!_segments.TryGetValue(segment.MessageId, out segments))
                {
                    segments = new SegmentList()
                    {
                        Created = CurrentTime.Now
                    };
                    var task = _scheduler.Schedule(segments.Created.Add(segment.TimeToLive),
                        (messageId) => { RemoveInboundMessage(messageId); },
                        () => segment.MessageId);
                    _tasks.Add(new ExpirationTask(segment.MessageId, task));
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
            _syncLock.Lock(() =>
            {
                _segments[messageId] = segments;
            });
        }

        protected virtual void UpdateMessageId(ushort instanceId, ulong messageId)
        {
            _syncLock.Lock(() =>
            {
                _messageIds[instanceId] = messageId;
            });
        }

        protected virtual void UpdateSegmentNumber(ulong messageId, ushort segmentNumber)
        {
            _syncLock.Lock(() =>
            {
                _segmentNumbers[messageId] = segmentNumber;
            });
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

            RemoveInboundMessage(message.MessageId);
            
        }

        protected virtual void RemoveInboundMessage(ulong messageId)
        {
            _syncLock.Lock(() =>
            {

                _segments.Remove(messageId);
                _segmentNumbers.Remove(messageId);

                var task = _tasks.SingleOrDefault(t => t.MessageId == messageId);
                if (task != null)
                {
                    task.Task.Cancel();
                    _tasks.Remove(task);
                }
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

        public virtual ulong RemoteMessageId(ushort instanceId)
        {
            return _syncLock.Lock(() => _messageIds[instanceId]);
        }

        public virtual void Reset()
        {
            _syncLock.Lock(() =>
            {
                if (!IsInitialized)
                    throw new InvalidOperationException("The channel buffer has not been initialized");
                _scheduler.TaskExpired -= OnTaskExpired;

                foreach (var item in _tasks)
                {
                    item.Task.Cancel();
                }
                _tasks.Clear();

                _messageIds.Clear(true);
                _messageIds.Dispose();
                //_segments.Clear(true);
                //_segments.Dispose();
                //_segmentNumbers.Clear(true);
                //_segmentNumbers.Dispose();
                //_sequenceNumbers.Clear();
                _segments.Clear();
                _segmentNumbers.Clear();

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
