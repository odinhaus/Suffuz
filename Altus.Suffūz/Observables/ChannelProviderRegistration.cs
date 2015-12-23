using System;

namespace Altus.Suffūz.Observables
{
    public class ChannelProviderRegistration : IDisposable
    {
        public ChannelProviderRegistration(IObservableChannelProvider provider)
        {
            Provider = provider;
        }

        public IObservableChannelProvider Provider { get; private set; }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}