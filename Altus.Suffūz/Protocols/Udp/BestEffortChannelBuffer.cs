using Altus.Suffūz.Collections;
using Altus.Suffūz.Scheduling;
using Altus.Suffūz.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols.Udp
{
    public class BestEffortChannelBuffer : IBestEffortChannelBuffer<UdpMessage>
    {
        IPersistentDictionary<ushort, ulong> _sequenceNumbers;
        IPersistentDictionary<ulong, UdpMessage> _resendBuffer;
        IChannel _channel;
        List<IScheduledTask> _expirations = new List<IScheduledTask>();
        IScheduler _scheduler;

        public BestEffortChannelBuffer(IChannel channel)
        {
            _channel = channel;
        }

        public void Initialize()
        {
            if (!IsInitialized)
            {
                var manager = App.Resolve<IManagePersistentCollections>();
                _scheduler = App.Resolve<IScheduler>();
                _scheduler.TaskExpired += TaskExpired;

                _sequenceNumbers = manager
                    .GetOrCreate<IPersistentDictionary<ushort, ulong>>(
                        _channel.Name + "_seq",
                        (name) => new PersistentDictionary<ushort, ulong>(name, manager.GlobalHeap, true));

                ulong sequenceNumber;
                if (!_sequenceNumbers.TryGetValue(App.InstanceId, out sequenceNumber))
                {
                    sequenceNumber = (ulong)((ulong)App.InstanceId << 48);
                    _sequenceNumbers[App.InstanceId] = sequenceNumber;
                }

                SequenceNumber = sequenceNumber;

                App.Resolve<ISerializationContext>()
                    .SetSerializer<UdpMessage, UdpMessageSerializer>(StandardFormats.BINARY);

                _resendBuffer = manager
                    .GetOrCreate<IPersistentDictionary<ulong, UdpMessage>>(
                        _channel.Name + "_nak",
                        (name) => new PersistentDictionary<ulong, UdpMessage>(name, manager.GlobalHeap, false));

                var now = CurrentTime.Now;
                foreach (var kvp in _resendBuffer)
                {
                    if (now < kvp.Value.UdpHeaderSegment.TimeToLive)
                    {
                        // set the message to expire from the retry buffer based on the message's TTL
                        _expirations.Add(_scheduler.Schedule<ulong>(kvp.Value.UdpHeaderSegment.TimeToLive,
                            (messageId) => _resendBuffer.Remove(messageId),
                            () => kvp.Value.MessageId));
                    }
                    else
                    {
                        _resendBuffer.Remove(kvp.Key);
                    }
                }
                IsInitialized = true;
            }
        }

        private void TaskExpired(object sender, TaskExpiredEventArgs e)
        {
            lock(_channel)
            {
                _expirations.Remove(e.Task);
            }
        }

        public void Reset()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("The channel buffer has not been initialized");

            _resendBuffer.Clear(true);
            _sequenceNumbers.Clear(true);
            _resendBuffer.Dispose();
            _sequenceNumbers.Dispose();

            foreach(var task in _expirations)
            {
                task.Cancel();
            }
            _expirations.Clear();

            IsInitialized = false;
            Initialize();
        }

        private ulong _seq;
        public ulong SequenceNumber
        {
            get
            {
                lock(_channel)
                {
                    return _seq;
                }
            }
            set
            {
                lock(_channel)
                {
                    _seq = value;
                }
            }
        }

        public void AddMessage(UdpMessage message)
        {
            lock(_channel)
            {
                if (message.UdpHeaderSegment.TimeToLive > CurrentTime.Now)
                {
                    // add message to retry buffer
                    _resendBuffer.Add(message.MessageId, message);

                    // set the message to expire from the retry buffer based on the message's TTL
                    _expirations.Add(_scheduler.Schedule<ulong>(message.UdpHeaderSegment.TimeToLive,
                        (messageId) => _resendBuffer.Remove(messageId),
                        () => message.MessageId));

                }
            }
        }

        public bool IsInitialized { get; private set; }
    }
}
