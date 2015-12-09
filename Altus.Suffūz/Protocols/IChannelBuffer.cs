using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols
{
    public delegate void MessageAvailableHandler<TMessage>(object sender, MessageAvailableEventArgs<TMessage> e);

    public class MessageAvailableEventArgs<TMessage>
    {
        public MessageAvailableEventArgs(TMessage message)
        {
            this.Message = message;
        }

        public TMessage Message { get; private set; }
    }

    public interface IChannelBuffer<TMessage>
    {
        /// <summary>
        /// Fired when a completed message is available for processing
        /// </summary>
        event MessageAvailableHandler<TMessage> MessageReceived;

        void AddInboundSegment(MessageSegment segment);
        /// <summary>
        /// The channel associated with the buffer
        /// </summary>
        IChannel Channel { get; }
        /// <summary>
        /// Indicates whether the buffer is ready for use
        /// </summary>
        bool IsInitialized { get; }
        /// <summary>
        /// Initializes the channel buffer internal buffers
        /// </summary>
        void Initialize(IChannel channel);
        /// <summary>
        /// Empties all buffers
        /// </summary>
        void Reset();
        /// <summary>
        /// Gets latest outbound Message Id for this channel
        /// </summary>
        ulong LocalMessageId { get; }
        /// <summary>
        /// Atomically adds 1 to the local message id for the associated channel
        /// </summary>
        /// <returns></returns>
        ulong IncrementLocalMessageId();
        /// <summary>
        /// Gets latest inbound message id from remote senders
        /// </summary>
        /// <param name="instanceId"></param>
        /// <returns></returns>
        ulong RemoteMessageId(ushort instanceId);
    }
}
