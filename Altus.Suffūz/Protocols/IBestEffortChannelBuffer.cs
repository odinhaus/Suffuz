namespace Altus.Suffūz.Protocols
{
    public interface IBestEffortChannelBuffer<TMessage>
    {
        ulong SequenceNumber { get; set; }
        bool IsInitialized { get; }

        void Initialize();
        void Reset();
        void AddMessage(TMessage message);
    }
}