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
    public class BestEffortChannelBuffer : ChannelBuffer, IBestEffortChannelBuffer<UdpMessage>
    {
        IPersistentDictionary<ulong, UdpMessage> _resendBuffer;

        List<Tuple<ulong, IScheduledTask>> _expirations = new List<Tuple<ulong, IScheduledTask>>();

        int _added = 0, _removed = 0;

        public BestEffortChannelBuffer() : base()
        {
        }

        public override void Initialize(IChannel channel)
        {
            if (!IsInitialized)
            {
                var manager = App.Resolve<IManagePersistentCollections>();

               

                _resendBuffer = manager
                    .GetOrCreate<IPersistentDictionary<ulong, UdpMessage>>(
                        Channel.Name + "_nak.bin",
                        (name) => new PersistentDictionary<ulong, UdpMessage>(name, manager.GlobalHeap, false));
                _resendBuffer.Compact();

                var now = CurrentTime.Now;
                foreach (var kvp in _resendBuffer)
                {
                    //if (now < kvp.Value.UdpHeaderSegment.TimeToLive)
                    //{
                    //    // set the message to expire from the retry buffer based on the message's TTL
                    //    _expirations.Add(new Tuple<ulong, IScheduledTask>(kvp.Value.MessageId,
                    //        _scheduler.Schedule<ulong>(kvp.Value.UdpHeaderSegment.TimeToLive,
                    //        (messageId) => _resendBuffer.Remove(messageId),
                    //        () => kvp.Value.MessageId)));
                    //    _added++;
                    //}
                    //else
                    //{
                    //    _resendBuffer.Remove(kvp.Key);
                    //    _removed++;
                    //}
                }


                base.Initialize(this.Channel);
            }
        }

        protected override void Compact()
        {
            lock(Channel)
            {
                if (_added != _removed)
                {
                    _resendBuffer.Compact();
                    _added = _removed = 0;
                }
            }
            base.Compact();
        }

        public override void Reset()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("The channel buffer has not been initialized");

            _resendBuffer.Clear(true);
            _resendBuffer.Dispose();

            foreach(var tuple in _expirations)
            {
                tuple.Item2.Cancel();
            }
            _expirations.Clear();

            base.Reset();
        }

        public void AddRetryMessage(UdpMessage message)
        {
            lock(Channel)
            {
                //if (message.UdpHeaderSegment.TimeToLive > CurrentTime.Now)
                //{
                //    // add message to retry buffer
                //    _resendBuffer.Add(message.MessageId, message);

                //    // set the message to expire from the retry buffer based on the message's TTL
                //    _expirations.Add(new Tuple<ulong, IScheduledTask>(message.MessageId,
                //        _scheduler.Schedule<ulong>(message.UdpHeaderSegment.TimeToLive,
                //        (messageId) => RemoveRetryMessage(messageId),
                //        () => message.MessageId)));
                //    _added++;
                //}
                //_sequenceNumbers.Add(message.Sender, message.MessageId);
            }
        }

        public void RemoveRetryMessage(UdpMessage message)
        {
            RemoveRetryMessage(message.MessageId);
        }

        public void RemoveRetryMessage(ulong messageId)
        {
            lock (Channel)
            {
                _resendBuffer.Remove(messageId);
                var tuple = _expirations.SingleOrDefault(t => t.Item1 == messageId);
                if (tuple != null)
                {
                    tuple.Item2.Cancel();
                    _expirations.Remove(tuple);
                }
                _removed++;
            }
        }

        public UdpMessage GetRetryMessage(ulong messageId)
        {
            lock (Channel)
            {
                try
                {
                    return _resendBuffer[messageId];
                }
                catch(KeyNotFoundException)
                {
                    return null;
                }
            }
        }

        public int RetryCount { get { return _resendBuffer.Count; } }

    }
}
