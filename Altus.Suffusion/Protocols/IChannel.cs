using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Protocols
{
    public interface IChannel : IDisposable
    {
        event EventHandler Disconnected;
        event EventHandler Disposing;
        event EventHandler Disposed;

        Protocol Protocol { get; }
        EndPoint EndPoint { get; }
        Encoding TextEncoding { get; set; }
        string Format { get; }
        Dictionary<string, object> ConnectionAspects { get; }

        TResponse Call<TResponse>(ChannelRequest request);
        void Call<TResponse>(ChannelRequest request, Func<TResponse, bool> handler);
        void Call<TRequest>(ChannelRequest<TRequest> request);
        TResponse Call<TRequest, TResponse>(ChannelRequest<TRequest> request);
        void Call<TRequest, TResponse>(ChannelRequest<TRequest> request, Func<TResponse, bool> handler);

        void SendError(Message message, Exception ex);
        void ResetProperties();
        bool IsDisconnected { get; }
    }
}
