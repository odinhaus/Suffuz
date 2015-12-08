using System.Collections.Generic;
using System.IO;

namespace Altus.Suffūz.Protocols
{
    public interface IProtocolMessage<TMessage> : IComparer<TMessage> where TMessage : MessageSegment
    {
        IChannel Connection { get; }
        bool IsComplete { get; }
        ulong MessageId { get; }
        Stream Payload { get; }
        ushort Sender { get; }
        ulong SequenceNumber { get; }
        void AddSegment(TMessage segment);

        byte[] ToBytes();
    }
}