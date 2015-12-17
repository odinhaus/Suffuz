using Altus.Suffūz.Protocols.Udp;
using System;

namespace Altus.Suffūz.Protocols
{
    public delegate void MissedSegmentsHandler(object sender, MissedSegmentsEventArgs e);

    public class MissedSegmentsEventArgs : EventArgs
    {
        public MissedSegmentsEventArgs(ushort senderId, ushort recipientId, ulong startSegment, ulong endSegment)
        {
            this.RecipientId = recipientId;
            this.SenderId = senderId;
            this.StartSegmentId = startSegment;
            this.EndSegmentId = endSegment;
        }

        public ulong EndSegmentId { get; private set; }
        public ushort RecipientId { get; private set; }
        public ushort SenderId { get; private set; }
        public ulong StartSegmentId { get; private set; }
    }

    public interface IBestEffortChannelBuffer<TMessage> : IChannelBuffer<TMessage>
    {
        event MissedSegmentsHandler MissedSegments;

        /// <summary>
        /// Adds a message segment to the NACK retry buffer
        /// </summary>
        /// <param name="segment"></param>
        void AddRetrySegment(MessageSegment segment);
        /// <summary>
        /// Removes a message segment from the NACK Retry buffer
        /// </summary>
        /// <param name="segment"></param>
        void RemoveRetrySegment(MessageSegment segment);
        /// <summary>
        /// Gets a message segment from the NACK Retry buffer
        /// </summary>
        /// <param name="segmentId"></param>
        /// <returns></returns>
        MessageSegment GetRetrySegement(ulong segmentId);

    }
}