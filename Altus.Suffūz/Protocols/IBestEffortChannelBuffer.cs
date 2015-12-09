namespace Altus.Suffūz.Protocols
{
    public interface IBestEffortChannelBuffer<TMessage> : IChannelBuffer<TMessage>
    {
        /// <summary>
        /// Number of NACK retry items available
        /// </summary>
        int RetryCount { get; }
        /// <summary>
        /// Adds a message to the NACK retry buffer
        /// </summary>
        /// <param name="message"></param>
        void AddRetryMessage(TMessage message);
        /// <summary>
        /// Removes a message from the NACK Retry buffer
        /// </summary>
        /// <param name="message"></param>
        void RemoveRetryMessage(TMessage message);
        /// <summary>
        /// Gets a message from the NACK Retry buffer
        /// </summary>
        /// <param name="messageId"></param>
        /// <returns></returns>
        TMessage GetRetryMessage(ulong messageId);
    }
}