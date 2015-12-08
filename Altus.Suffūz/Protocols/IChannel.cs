using Altus.Suffūz.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols
{
    public interface IChannel : IDisposable
    {
        event EventHandler Disconnected;
        event EventHandler Disposing;
        event EventHandler Disposed;

        string Name { get; }
        Protocol Protocol { get; }
        EndPoint EndPoint { get; }
        Encoding TextEncoding { get; set; }
        string Format { get; }
        TResponse Call<TRequest, TResponse>(ChannelRequest<TRequest, TResponse> request);
        TResponse Call<TRequest, TResponse>(ChannelRequest<TRequest, TResponse> request, Func<TResponse, bool> handler);
        void ResetProperties();
        bool IsDisconnected { get; }
        ServiceLevels ServiceLevels { get; }
        TimeSpan DefaultTimeout { get; set; }
        ulong SequenceNumber { get; }
    }
}
