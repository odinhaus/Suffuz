﻿using Altus.Suffusion.Diagnostics;
using Altus.Suffusion.IO;
using Altus.Suffusion.Routing;
using Altus.Suffusion.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Altus.Suffusion.Protocols.Udp
{
    public delegate void DataReceivedHandler(object sender, DataReceivedArgs e);

    public class DataReceivedArgs : EventArgs
    {
        public DataReceivedArgs(byte[] buffer, int length, EndPoint source, EndPoint destination)
        {
            this.Buffer = buffer;
            this.Length = length;
            this.SourceEndPoint = source;
            this.DestinationEndPoint = destination;
        }
        public byte[] Buffer { get; private set; }
        public int Length { get; private set; }
        public EndPoint SourceEndPoint { get; private set; }
        public EndPoint DestinationEndPoint { get; set; }
    }

    public class MulticastChannel : IChannel, IDisposable
    {
        public event SocketExceptionHandler SocketException;
        public bool IsDisconnected { get; protected set; }
        public event EventHandler Disconnected;
        protected void OnDisconnected()
        {
            IsDisconnected = true;
            if (Disconnected != null)
                Disconnected(this, new EventArgs());
        }

        private static PerformanceCounter BytesSentRate;
        private static PerformanceCounter BytesReceivedRate;
        private static PerformanceCounter MsgSentRate;
        private static PerformanceCounter MsgReceivedRate;
        private static PerformanceCounter BytesSentTotal;

        static MulticastChannel()
        {
            BytesSentRate = PerformanceCounter.GetPerfCounterInstance(PerformanceCounterNames.UDP_RateOfBytesSent_NAME);
            BytesSentTotal = PerformanceCounter.GetPerfCounterInstance(PerformanceCounterNames.UDP_NUMBEROFBYTESSENT_NAME);
            BytesReceivedRate = PerformanceCounter.GetPerfCounterInstance(PerformanceCounterNames.UDP_RateOfBytesReceived_NAME);

            MsgReceivedRate = PerformanceCounter.GetPerfCounterInstance(PerformanceCounterNames.UDP_RateOfMessagesReceived_NAME);
            MsgSentRate = PerformanceCounter.GetPerfCounterInstance(PerformanceCounterNames.UDP_RateOfMessagesSent_NAME);
        }

        static Dictionary<string, object> _locks = new Dictionary<string, object>();
        object _lock;
        IServiceRouter _router;

        public MulticastChannel(IPEndPoint mcastGroup, bool listen) : this(mcastGroup, listen, true)
        {

        }

        public MulticastChannel(IPEndPoint mcastGroup, bool listen, bool excludeMessagesFromSelf)
            : this(new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp), mcastGroup, listen, excludeMessagesFromSelf)
        {
        }

        public MulticastChannel(Socket udpSocket, IPEndPoint mcastGroup, bool listen, bool excludeMessagesFromSelf)
        {
            this.DataReceivedHandler = new DataReceivedHandler(this.DefaultDataReceivedHandler);
            this.ExcludeMessagesFromSelf = excludeMessagesFromSelf;
            this.Socket = udpSocket;
            this.Socket.SendBufferSize = 8192 * 2;
            this.EndPoint = IPEndPointEx.LocalEndPoint(mcastGroup.Port, true);
            this.Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            this.Socket.ExclusiveAddressUse = false;
            this.Socket.Bind(this.EndPoint);
            this.McastEndPoint = mcastGroup;
            this.JoinGroup(listen);
            lock (_locks)
            {
                if (!_locks.ContainsKey(mcastGroup.ToString()))
                {
                    _locks.Add(mcastGroup.ToString(), new object());
                }
            }
            _lock = _locks[mcastGroup.ToString()];
            this.TextEncoding = Encoding.Unicode;
            this.Cleaner = new Timer(new TimerCallback(CleanInboundOrphans), null, 1000, 1000);
            this._router = App.Resolve<IServiceRouter>();
        }

        public MulticastChannel(IPEndPoint mcastGroup, bool listen, DataReceivedHandler handler)
            : this(mcastGroup, listen, true, handler)
        {

        }
        public MulticastChannel(IPEndPoint mcastGroup, bool listen, bool excludeMessagesFromSelf, DataReceivedHandler handler)
            : this(new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp), mcastGroup, listen, excludeMessagesFromSelf, handler)
        {
        }


        public MulticastChannel(Socket udpSocket, IPEndPoint mcastGroup, bool listen, bool excludeMessagesFromSelf, DataReceivedHandler handler)
        {
            if (handler == null) throw new ArgumentException("DataReceivedHandler cannot be null.");

            this.DataReceivedHandler = handler;
            this.ExcludeMessagesFromSelf = excludeMessagesFromSelf;
            this.Socket = udpSocket;
            this.Socket.SendBufferSize = 8192 * 2;
            this.EndPoint = IPEndPointEx.LocalEndPoint(mcastGroup.Port, true); //new IPEndPoint(IPAddress.Any, mcastGroup.Port);
            this.Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            this.Socket.ExclusiveAddressUse = false;
            this.Socket.Bind(this.EndPoint);
            this.McastEndPoint = mcastGroup;
            this.JoinGroup(listen);
            lock (_locks)
            {
                if (!_locks.ContainsKey(mcastGroup.ToString()))
                {
                    _locks.Add(mcastGroup.ToString(), new object());
                }
            }
            _lock = _locks[mcastGroup.ToString()];
            this.TextEncoding = Encoding.Unicode;
            this.Cleaner = new Timer(new TimerCallback(CleanInboundOrphans), null, 1000, 1000);
            this._router = App.Resolve<IServiceRouter>();
        }

        private DataReceivedHandler DataReceivedHandler;

        private System.Threading.Timer Cleaner;
        private void CleanInboundOrphans(object state)
        {
            Guid[] orphans = new Guid[0];
            System.DateTime now = CurrentTime.Now;
            lock (this._udpInboundMessages)
            {
                try
                {
                    orphans = this._udpInboundMessages
                        .Where(kvp => kvp.Value.UdpSegments.Length > 0 && kvp.Value.UdpSegments.Count(s => s.TimeToLive >= now) > 0)
                        .Select(kvp => kvp.Key).ToArray();
                }
                catch { }
            }

            for (int i = 0; i < orphans.Length; i++)
            {
                this._udpInboundMessages.Remove(orphans[i]);
            }
        }

        public void Send(byte[] data)
        {
            lock (_lock)
            {
                Socket.SendTo(data, this.McastEndPoint);
                BytesSentRate.IncrementBy(data.Length);
                BytesSentTotal.IncrementBy(data.Length);
            }
        }

        public void Send(Message message)
        {
            if (this.TextEncoding == null)
                if (message.Encoding != null)
                    this.TextEncoding = Encoding.GetEncoding(message.Encoding);
                else
                    this.TextEncoding = Encoding.Unicode;

            if (message.Encoding == null)
                message.Encoding = this.TextEncoding.EncodingName;

            App.Resolve<ISerializationContext>().TextEncoding = Encoding.GetEncoding(message.Encoding);
            UdpMessage tcpMsg = new UdpMessage(this, message);

            this.Send(tcpMsg.UdpHeaderSegment.Data);
            for (int i = 0; i < tcpMsg.UdpSegments.Length; i++)
            {
                this.Send(tcpMsg.UdpSegments[i].Data);
            }

            MsgSentRate.IncrementByFast(1);
            this.ConnectionAspects = new Dictionary<string, object>();
        }

        public void SendError(Message message, Exception ex)
        {
            throw (new NotImplementedException());
        }

        static Dictionary<string, MessageReceivedHandler> _receivers = new Dictionary<string, MessageReceivedHandler>();
        //public ServiceOperation Call(string application, string objectPath, string operation, string format, TimeSpan timespan, params ServiceParameter[] parms)
        //{
        //    string uri = string.Format(
        //        "udp://{0}/{1}/{2}({3})[{4}]",
        //        this.McastEndPoint.ToString(),
        //        application,
        //        objectPath,
        //        operation,
        //        format);
        //    ServiceOperation op = new ServiceOperation(OperationType.Request, ServiceType.RequestResponse, uri, parms);

        //    IConnection connection = this;
        //    Message msg = new Message(op);

        //    Message resp = connection.Call(msg, timespan);

        //    ISerializer serializer = App.Resolve<ISerializationContext>().GetSerializer(TypeHelper.GetType(resp.PayloadType), resp.PayloadFormat);
        //    if (serializer == null)
        //        throw (new SerializationException("Deserializer for " + resp.PayloadType + " in " + resp.PayloadFormat + " format could not be found."));
        //    object value = serializer.Deserialize(StreamHelper.GetBytes(resp.PayloadStream), TypeHelper.GetType(resp.PayloadType));
        //    if (value is ServiceOperation)
        //    {
        //        return value as ServiceOperation;
        //    }
        //    else if (value is ServiceParameterCollection)
        //    {
        //        ServiceOperation so = new ServiceOperation(resp, OperationType.Response);
        //        so.Parameters.AddRange(value as ServiceParameterCollection);
        //        return so;
        //    }
        //    else
        //        throw (new InvalidOperationException("Return type not supported"));
        //}

        public Message Call(Message message)
        {
            return this.Call(message, 30000);
        }

        public Message Call(Message message, int timespan)
        {
            return this.Call(message, TimeSpan.FromMilliseconds(timespan));
        }

        public Message Call(Message message, TimeSpan timespan)
        {
            AsyncRequest async = new AsyncRequest(message, timespan);

            if (async.CanHaveResponse)
            {
                lock (_receivers)
                {
                    _receivers.Add(message.Id, async.ResponseCallback);
                }
            }

            this.Send(message);

            return async.GetResponse();
        }

        public async Task CallAsync<TRequest>(ChannelRequest<TRequest> request)
        {
            await Task.Run(() =>
            {
                var message = new Message(Format, request.Uri, ServiceType.RequestResponse, App.InstanceName)
                {
                    Payload = request.Payload,
                    Recipients = request.Recipients
                };
                Call(message, request.Timeout);
            });
        }

        public async Task<TResponse> CallAsync<TResponse>(ChannelRequest request)
        {
            return await CallAsync<NoArgs, TResponse>((ChannelRequest<NoArgs>)request);
        }

        public async Task<TResponse> CallAsync<TRequest, TResponse>(ChannelRequest<TRequest> request)
        {
            return await Task.Run<TResponse>(() =>
            {
                var message = new Message(Format, request.Uri, ServiceType.RequestResponse, App.InstanceName)
                {
                    Payload = request.Payload,
                    Recipients = request.Recipients
                };

                var response = Call(message, request.Timeout);
                
                return (TResponse)response.Payload;
            });
        }

        public async Task CallAsync<TResponse>(ChannelRequest request, Func<TResponse, bool> handler)
        {
            await CallAsync<NoArgs, TResponse>((ChannelRequest<NoArgs>)request, handler);
        }

        public async Task CallAsync<TRequest, TResponse>(ChannelRequest<TRequest> request, Func<TResponse, bool> handler)
        {
            await Task.Run(() =>
            {
                var message = new Message(Format, request.Uri, ServiceType.Broadcast, App.InstanceName)
                {
                    Payload = request.Payload,
                    Recipients = request.Recipients
                };

                Call<TResponse>(message, request.Timeout, handler);
            });
        }

        public void Call<U>(Message message, TimeSpan timeout, Func<U, bool> handler)
        {
            var evt = new ManualResetEvent(handler == null); // if no handler, we just move on through after sending, no blocks
            lock (_receivers)
            {
                _receivers.Add(message.Id, (sender, response) =>
                {
                    if (handler((U)response.Payload))
                    {
                        evt.Set(); // it's been handled, so stop receiving
                    }
                });
            }

            Send(message);

            evt.WaitOne(timeout); // block for up to timeout

            lock (_receivers)
            {
                _receivers.Remove(message.Id); // clean up, we're done, ignore any other messages that arrive 
            }
        }

        public Socket Socket { get; private set; }
        public EndPoint EndPoint { get; set; }
        public EndPoint McastEndPoint { get; set; }
        public Protocol Protocol { get { return Protocol.Udp; } }
        public Action Action { get { return Action.POST; } }
        public bool ExcludeMessagesFromSelf { get; private set; }

        [ThreadStatic()]
        static Dictionary<string, object> _aspects;
        public Dictionary<string, object> ConnectionAspects { get { return _aspects; } set { _aspects = value; } }

        [ThreadStatic()]
        static Encoding _encoding;
        public Encoding TextEncoding
        {
            get { return _encoding; }
            set { _encoding = value; }
        }

        public void ResetProperties()
        {
            _aspects = new Dictionary<string, object>();
            _encoding = null;
        }

        public string Format { get { return StandardFormats.BINARY; } }

        public void JoinGroup(bool listen)
        {
            this.Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
            Logger.LogInfo("Joining Multicast Group: " + this.McastEndPoint.ToString() + ", on Local Address: " + this.EndPoint.ToString());
            this.Socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(((IPEndPoint)this.McastEndPoint).Address, ((IPEndPoint)this.EndPoint).Address));
            this.Socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
            this.Socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
            if (listen)
            {
                ReadMessages();
            }
        }

        public void LeaveGroup()
        {
            try
            {
                Logger.LogInfo("Leaving Multicast Group: " + this.McastEndPoint.ToString() + ", from Local Address: " + this.EndPoint.ToString());
                this.Socket.SetSocketOption(SocketOptionLevel.IP,
                               SocketOptionName.DropMembership,
                               new MulticastOption(((IPEndPoint)this.McastEndPoint).Address, IPEndPointEx.LocalAddress(true)));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred leaving Multicast Group: " + this.McastEndPoint.ToString() + ", from Local Address: " + this.EndPoint.ToString());
            }
        }

        Thread _receiveThread;
        protected void ReadMessages()
        {
            _receiveThread = new Thread(new ThreadStart(ReadMessagesLoop));
            _receiveThread.Name = "UDP Multicast Listener [" + this.McastEndPoint.ToString() + "]";
            _receiveThread.Priority = ThreadPriority.Highest;
            _receiveThread.IsBackground = true;
            _receiveThread.Start();

        }

        private void DefaultDataReceivedHandler(object sender, DataReceivedArgs e)
        {
            MessageSegment segment;
            if (MessageSegment.TryCreate(this, Protocol.Udp, EndPoint, e.Buffer, out segment))
                ProcessInboundUdpSegment(segment);
        }

        private void ReadMessagesLoop()
        {
            while (!disposed)
            {
                try
                {
                    byte[] buffer = new byte[SocketOptions.BUFFER_SIZE];
                    EndPoint ep = this.McastEndPoint;
                    int read = this.Socket.ReceiveFrom(buffer, ref ep);

                    BytesReceivedRate.IncrementBy(read);
                    if (read > 0)
                    {
                        this.DataReceivedHandler(this, new DataReceivedArgs(buffer, read, ep, this.McastEndPoint));
                    }
                    else
                    {
                        this.OnSocketException(new IOException("An existing connection was closed by the remote host."));
                        break;
                    }

                }
                catch (ThreadAbortException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if ((this.Socket != null) && (this.Socket.Connected == true))
                    {
                        Logger.LogInfo("Trying to release the Socket since it's been severed");
                        // Release the socket.
                        this.Socket.Shutdown(SocketShutdown.Both);
                        this.Socket.Disconnect(true);
                        if (this.Socket.Connected)
                            Logger.LogError("We're still connnected!!!!");
                        else
                            Logger.LogInfo("We're disconnected");
                    }
                    this.OnSocketException(ex);
                    break;
                }
            }
        }

        Dictionary<Guid, UdpMessage> _udpInboundMessages = new Dictionary<Guid, UdpMessage>();
        private void ProcessInboundUdpSegment(MessageSegment segment)
        {
            if (segment.SegmentType == SegmentType.Segment)
            {
                // segment
                ProcessInboundUdpSegmentSegment((UdpSegment)segment);
            }
            else
            {
                // header
                ProcessInboundUdpHeaderSegment((UdpHeader)segment);
            }
        }

        private void ProcessInboundUdpSegmentSegment(UdpSegment segment)
        {
            UdpMessage msg = null;
            if (segment.MessageId == Guid.Empty) return; // discard bad message
            lock (_udpInboundMessages)
            {
                try
                {
                    if (_udpInboundMessages.ContainsKey(segment.MessageId))
                    {
                        msg = _udpInboundMessages[segment.MessageId];
                        msg.AddSegment(segment);
                    }
                    else
                    {
                        //key not found
                        msg = new UdpMessage(segment.Connection, segment);
                        msg.AddSegment(segment);

                        _udpInboundMessages.Add(msg.MessageId, msg);
                    }
                }
                catch
                {

                }

                if (msg != null && msg.IsComplete)
                {
                    try
                    {
                        ProcessCompletedInboundUdpMessage(msg);
                    }
                    finally
                    {
                        lock (_udpInboundMessages)
                        {
                            _udpInboundMessages.Remove(segment.MessageId);
                        }
                    }
                }
            }
        }

        private void ProcessInboundUdpHeaderSegment(UdpHeader segment)
        {
            UdpMessage msg = null;
            if (segment.MessageId == Guid.Empty) return; // discard bad message
            lock (_udpInboundMessages)
            {
                try
                {
                    // udp can duplicate messages, or send payload datagrams ahead of the header
                    if (_udpInboundMessages.ContainsKey(segment.MessageId))
                    {
                        msg = _udpInboundMessages[segment.MessageId];
                        msg.UdpHeaderSegment = segment;
                    }
                    else
                    {
                        msg = new UdpMessage(segment.Connection, segment);
                        lock (_udpInboundMessages)
                        {
                            _udpInboundMessages.Add(msg.MessageId, msg);
                        }
                    }
                }
                catch
                {

                }

                if (msg != null && msg.IsComplete)
                {
                    try
                    {
                        ProcessCompletedInboundUdpMessage(msg);
                    }
                    finally
                    {
                        lock (_udpInboundMessages)
                        {
                            _udpInboundMessages.Remove(segment.MessageId);
                        }
                    }
                }
            }
        }

        private void ProcessCompletedInboundUdpMessage(UdpMessage udpMessage)
        {
            this.ConnectionAspects = new Dictionary<string, object>();

            Message message = (Message)Message.FromStream(udpMessage.Payload);
            App.Resolve<ISerializationContext>().TextEncoding = System.Text.Encoding.Unicode;
            ProcessInboundMessage(message);
            MsgReceivedRate.IncrementByFast(1);
        }

        private void ProcessInboundMessage(Message message)
        {
            //Console.WriteLine("Received {0} From {1}", message.Payload.GetType().Name, message.Sender);
            MessageReceivedHandler callback;
            bool hasCallback = false;
            lock(_receivers)
            {
                hasCallback = _receivers.TryGetValue(message.CorrelationId, out callback);
            }
            if (hasCallback)
            {
                try
                {
                    callback(this, message);
                }
                catch { }
                finally
                {
                    if (message.ServiceType == ServiceType.RequestResponse)
                    {
                        lock(_receivers)
                        {
                            // we only care about one response, so remove the callback
                            _receivers.Remove(message.CorrelationId);
                        }
                    }
                }
            }
            else if (string.IsNullOrEmpty(message.CorrelationId))
            {
                if ((ExcludeMessagesFromSelf && message.Sender.Equals(App.InstanceName, StringComparison.InvariantCultureIgnoreCase)))
                    return; // don't process our own mutlicast publications

                if (!message.Recipients.Any(r => r.Equals("*") || r.Equals(App.InstanceName)))
                    return;

                var payloadType = TypeHelper.GetType(message.PayloadType);
                var route = _router.GetRoute(message.ServiceUri, payloadType);
                if (route != null)
                {
                    var result = route.HasParameters 
                        ? route.Handler.DynamicInvoke(message.Payload) 
                        : route.Handler.DynamicInvoke();
                    if ((message.ServiceType == ServiceType.RequestResponse || message.ServiceType == ServiceType.Broadcast)
                        && route.Handler.Method.ReturnType != typeof(void))
                    {
                        var response = new Message(Format, message.ServiceUri, message.ServiceType, App.InstanceName)
                        {
                            Payload = result,
                            CorrelationId = message.Id,
                            DeliveryGuaranteed = message.DeliveryGuaranteed,
                            Recipients = new string[] { message.Sender },
                            Timestamp = CurrentTime.Now,
                            IsReponse = true
                        };
                        this.Send(response);
                        //Console.WriteLine("Sent {0} To {1}", result.GetType().Name, message.Sender);
                    }
                }
                // for multicast listeners, the absence of a matched route doesn't mean there's a problem, as the listener may see many
                // messages on the group that simply aren't relevant, so we don't throw an exception here - we just ignore
            }
        }

        protected virtual void OnSocketException(Exception e)
        {
            if (this.SocketException != null)
            {
                this.SocketException(this, new SocketExceptionEventArgs(this, e));
            }
            OnDisconnected();
        }

        #region IDisposable Members
        bool disposed = false;

        // Implement IDisposable.
        // Do not make this method virtual.
        // A derived class should not be able to override this method.
        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        public event EventHandler Disposing;
        public event EventHandler Disposed;
        //========================================================================================================//
        // Dispose(bool disposing) executes in two distinct scenarios.
        // If disposing equals true, the method has been called directly
        // or indirectly by a user's code. Managed and unmanaged resources
        // can be disposed.
        // If disposing equals false, the method has been called by the 
        // runtime from inside the finalizer and you should not reference 
        // other objects. Only unmanaged resources can be disposed.
        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                if (this.Disposing != null)
                    this.Disposing(this, new EventArgs());
                // If disposing equals true, dispose all managed 
                // and unmanaged resources.
                if (disposing)
                {
                    // Dispose managed resources.
                    this.OnDisposeManagedResources();
                }

                // Call the appropriate methods to clean up 
                // unmanaged resources here.
                // If disposing is false, 
                // only the following code is executed.
                this.OnDisposeUnmanagedResources();
                if (this.Disposed != null)
                    this.Disposed(this, new EventArgs());
            }
            disposed = true;
        }

        /// <summary>
        /// Dispose managed resources
        /// </summary>
        protected virtual void OnDisposeManagedResources()
        {
            lock (this)
            {
                try
                {
                    if (_receiveThread != null)
                    {
                        _receiveThread.Abort();
                        _receiveThread = null;
                    }
                    this.LeaveGroup();
                    this.Socket.Close();
                    this.Socket.Dispose();
                    if (this.Cleaner != null)
                    {
                        this.Cleaner.Dispose();
                        this.Cleaner = null;
                    }
                    OnDisconnected();
                }
                catch { }
            }
        }

        /// <summary>
        /// Dispose unmanaged (native resources)
        /// </summary>
        protected virtual void OnDisposeUnmanagedResources()
        {
        }

        #endregion
    }
}