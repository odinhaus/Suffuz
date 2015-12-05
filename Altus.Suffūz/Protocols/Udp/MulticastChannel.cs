using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Altus.Suffūz.Diagnostics;
using Altus.Suffūz.Routing;
using Altus.Suffūz.Serialization;
using Altus.Suffūz.Messages;
using Altus.Suffūz.Scheduling;

namespace Altus.Suffūz.Protocols.Udp
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
            this.SequenceNumber = (ulong)(App.InstanceId << 48);
            this.DataReceivedHandler = new DataReceivedHandler(this.DefaultDataReceivedHandler);
            this.ExcludeSelf = excludeMessagesFromSelf;
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
            Scheduler.Current.Schedule(2000, () => Cleaner());
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
            this.SequenceNumber = (ulong)(App.InstanceId << 48);
            this.DataReceivedHandler = handler;
            this.ExcludeSelf = excludeMessagesFromSelf;
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
            Scheduler.Current.Schedule(2000, () => Cleaner());
            this._router = App.Resolve<IServiceRouter>();
        }

        protected DataReceivedHandler DataReceivedHandler;

        protected virtual void Cleaner()
        {
            ulong[] orphans = new ulong[0];
            System.DateTime now = CurrentTime.Now;
            lock (this._messages)
            {
                try
                {
                    orphans = this._messages
                        .Where(kvp => kvp.Value.UdpSegments.Length > 0 && kvp.Value.UdpSegments.Count(s => s.TimeToLive >= now) > 0)
                        .Select(kvp => kvp.Key).ToArray();
                }
                catch { }
            }

            for (int i = 0; i < orphans.Length; i++)
            {
                this._messages.Remove(orphans[i]);
            }
        }

        public virtual void Send(byte[] data)
        {
            lock (SyncRoot)
            {
                Socket.SendTo(data, this.McastEndPoint);
                BytesSentRate.IncrementBy(data.Length);
                BytesSentTotal.IncrementBy(data.Length);
            }
        }

        public virtual void Send(Message message)
        {
            if (this.TextEncoding == null)
                if (message.Encoding != null)
                    this.TextEncoding = Encoding.GetEncoding(message.Encoding);
                else
                    this.TextEncoding = Encoding.Unicode;

            if (message.Encoding == null)
                message.Encoding = this.TextEncoding.EncodingName;

            App.Resolve<ISerializationContext>().TextEncoding = Encoding.GetEncoding(message.Encoding);
            UdpMessage tcpMsg = CreateUdpMessage(message);

            this.Send(tcpMsg.UdpHeaderSegment.Data);

            for (int i = 0; i < tcpMsg.UdpSegments.Length; i++)
            {
                this.Send(tcpMsg.UdpSegments[i].Data);
            }

            MsgSentRate.IncrementByFast(1);
        }

        public virtual UdpMessage CreateUdpMessage(Message message)
        {
            lock (SyncRoot)
            {
                SequenceNumber++;
                return new UdpMessage(this, message);
            }
        }

        public virtual void SendError(Message message, Exception ex)
        {
            throw (new NotImplementedException());
        }

        static Dictionary<string, MessageReceivedHandler> _receivers = new Dictionary<string, MessageReceivedHandler>();

        public virtual Message Call(Message message)
        {
            return this.Call(message, 30000);
        }

        public virtual Message Call(Message message, int timespan)
        {
            return this.Call(message, TimeSpan.FromMilliseconds(timespan));
        }

        public virtual Message Call(Message message, TimeSpan timespan)
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


        public virtual TResponse Call<TRequest, TResponse>(ChannelRequest<TRequest, TResponse> request)
        {
            if (typeof(TResponse) == typeof(NoReturn))
            {
                var message = new Message(Format, request.Uri, ServiceType.Broadcast, App.InstanceName)
                {
                    Payload = new RoutablePayload(request.Payload, typeof(TRequest), typeof(TResponse)),
                    Recipients = request.Recipients,
                    TTL = this.TTL
                };

                Call(message, TimeSpan.FromMilliseconds(0));
                return default(TResponse);
            }
            else
            {
                bool isNomination = request.Payload is NominateExecutionRequest;
                object deferredPayload = null;
                if (isNomination)
                {
                    // for scalar nominations, we don't want to send the actual request payload twice
                    // so we strip it here, and hold it
                    deferredPayload = ((NominateExecutionRequest)(object)request.Payload).Request;
                    ((NominateExecutionRequest)(object)request.Payload).Request = NoArgs.Empty;
                    ((NominateExecutionRequest)(object)request.Payload).IsPayloadDeferred = true;
                    ((NominateExecutionRequest)(object)request.Payload).RequestType = deferredPayload.GetType().AssemblyQualifiedName;
                }

                var message = new Message(Format, request.Uri, ServiceType.RequestResponse, App.InstanceName)
                {
                    Payload = new RoutablePayload(request.Payload, typeof(TRequest), typeof(TResponse)),
                    Recipients = request.Recipients
                };

                var response = Call(message, request.Timeout);

                if (isNomination)
                {
                    // now send the actual payload to first respondant
                    message = new Message(Format, request.Uri, ServiceType.RequestResponse, App.InstanceName)
                    {
                        Payload = new RoutablePayload(deferredPayload, deferredPayload.GetType(), typeof(TResponse)),
                        Recipients = new string[] { response.Sender }
                    };

                    response = Call(message, request.Timeout);
                }

                return (TResponse)response.Payload;
            }
        }


        public virtual void Call<TRequest, TResponse>(ChannelRequest<TRequest, TResponse> request, Func<TResponse, bool> handler)
        {
            var message = new Message(Format, request.Uri, ServiceType.Broadcast, App.InstanceName)
            {
                Payload = new RoutablePayload(request.Payload, typeof(TRequest), typeof(TResponse)),
                Recipients = request.Recipients,
                TTL = this.TTL
            };

            Call<TResponse>(message, typeof(TResponse) == typeof(NoReturn) ? TimeSpan.FromMilliseconds(0) : request.Timeout, handler);
        }

        public virtual void Call<U>(Message message, TimeSpan timeout, Func<U, bool> handler)
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
        public bool ExcludeSelf { get; private set; }
        public virtual ServiceLevels ServiceLevels { get { return ServiceLevels.Default; } }
        public virtual TimeSpan TTL { get { return TimeSpan.FromSeconds(0); } set { } }
        public object SyncRoot { get { return _lock; } }
        public ulong SequenceNumber { get; protected set; }

        [ThreadStatic()]
        static Encoding _encoding;
        public Encoding TextEncoding
        {
            get { return _encoding; }
            set { _encoding = value; }
        }

        public virtual void ResetProperties()
        {
            _encoding = null;
        }

        public string Format { get { return StandardFormats.BINARY; } }

        public virtual void JoinGroup(bool listen)
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

        public virtual void LeaveGroup()
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
        protected virtual void ReadMessages()
        {
            _receiveThread = new Thread(new ThreadStart(ReadMessagesLoop));
            _receiveThread.Name = "UDP Multicast Listener [" + this.McastEndPoint.ToString() + "]";
            _receiveThread.Priority = ThreadPriority.Highest;
            _receiveThread.IsBackground = true;
            _receiveThread.Start();

        }

        protected virtual void DefaultDataReceivedHandler(object sender, DataReceivedArgs e)
        {
            MessageSegment segment;
            if (MessageSegment.TryCreate(this, Protocol.Udp, EndPoint, e.Buffer, out segment))
                ProcessInboundUdpSegment(segment);
        }

        protected virtual void ReadMessagesLoop()
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
                    this.OnSocketException(ex);
                }
            }
        }

        protected Dictionary<ulong, UdpMessage> _messages = new Dictionary<ulong, UdpMessage>();
        protected virtual void ProcessInboundUdpSegment(MessageSegment segment)
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

        protected virtual void ProcessInboundUdpSegmentSegment(UdpSegment segment)
        {
            UdpMessage msg = null;
            if (segment.MessageId == 0) return; // discard bad message
            lock (_messages)
            {
                try
                {
                    if (_messages.TryGetValue(segment.MessageId, out msg))
                    {
                        msg.AddSegment(segment);
                    }
                    else
                    {
                        msg = new UdpMessage(segment.Connection, segment);
                        msg.AddSegment(segment);

                        _messages.Add(msg.MessageId, msg);
                    }
                }
                catch
                {

                }

                if (msg != null && msg.IsComplete)
                {
                    _messages.Remove(segment.MessageId);
                    ProcessCompletedInboundUdpMessage(msg);
                }
            }
        }

        protected virtual void ProcessInboundUdpHeaderSegment(UdpHeader segment)
        {
            UdpMessage msg = null;
            if (segment.MessageId == 0) return; // discard bad message
            lock (_messages)
            {
                try
                {
                    // udp can duplicate messages, or send payload datagrams ahead of the header
                    if (_messages.TryGetValue(segment.MessageId, out msg))
                    {
                        msg.UdpHeaderSegment = segment;
                    }
                    else
                    {
                        msg = new UdpMessage(segment.Connection, segment);
                        _messages.Add(msg.MessageId, msg);
                    }
                }
                catch
                {

                }

                if (msg != null && msg.IsComplete)
                {
                    _messages.Remove(segment.MessageId);
                    ProcessCompletedInboundUdpMessage(msg);
                }
            }
        }

        protected virtual void ProcessCompletedInboundUdpMessage(UdpMessage udpMessage)
        {
            Message message = (Message)Message.FromStream(udpMessage.Payload);
            App.Resolve<ISerializationContext>().TextEncoding = System.Text.Encoding.Unicode;
            ProcessInboundMessage(message);
            MsgReceivedRate.IncrementByFast(1);
        }

        protected virtual void ProcessInboundMessage(Message message)
        {
            MessageReceivedHandler callback;
            bool hasCallback = false;
            lock(_receivers)
            {
                hasCallback = _receivers.TryGetValue(message.CorrelationId, out callback);
            }
            if (hasCallback)
            {
                HandleCallback(callback, message);
            }
            else if (string.IsNullOrEmpty(message.CorrelationId))
            {
                if ((ExcludeSelf && message.Sender.Equals(App.InstanceName, StringComparison.InvariantCultureIgnoreCase)))
                    return; // don't process our own mutlicast publications

                if (!message.Recipients.Any(r => r.Equals("*") || r.Equals(App.InstanceName)))
                    return;

                HandleNewMessage(message);
            }
        }

        protected virtual void HandleNewMessage(Message message)
        {
            var payloadType = TypeHelper.GetType(message.PayloadType);
            var requestType = payloadType;
            var responseType = typeof(NoReturn);
            var isScalar = false;
            object payload = message.Payload;
            Func<NominateResponse, bool> capacityPredicate = null;

            if (payloadType.IsTypeOrSubtypeOf<RoutablePayload>())
            {
                payloadType = TypeHelper.GetType(((RoutablePayload)payload).PayloadType);
                responseType = TypeHelper.GetType(((RoutablePayload)payload).ReturnType);
                payload = ((RoutablePayload)payload).Payload;
            }

            if (IsDelegatedRequest(payload, payloadType, out requestType))
            {
                capacityPredicate = new Serialization.Expressions.ExpressionSerializer()
                    .Deserialize<Func<NominateResponse, bool>>(XElement.Parse(((NominateExecutionRequest)payload).Nominator))
                    .Compile();
                isScalar = ((NominateExecutionRequest)payload).ScalarResults;
                payload = ((NominateExecutionRequest)payload).Request;
            }

            var route = _router.GetRoute(message.ServiceUri, requestType, responseType);
            if (route != null)
            {
                if (capacityPredicate != null)
                {
                    // we need to evaluate whether the route should be executed
                    var nomination = route.Nominate();
                    if (capacityPredicate(nomination))
                    {
                        var delay = route.Delay(nomination);
                        if (delay.TotalMilliseconds > 0)
                        {
                            Thread.Sleep(delay);
                        }

                        if (isScalar)
                        {
                            // return the nomination message, so the requestor can select a single agent to handle the request
                            var response = new Message(Format, message.ServiceUri, message.ServiceType, App.InstanceName)
                            {
                                Payload = nomination,
                                CorrelationId = message.Id,
                                ServiceLevel = message.ServiceLevel,
                                Recipients = new string[] { message.Sender },
                                Timestamp = CurrentTime.Now,
                                IsReponse = true
                            };
                            this.Send(response);
                            return;
                        }
                    }
                    else return; // we didn't pass the test, so don't process the request
                }

                var result = route.HasParameters
                    ? route.Handler.DynamicInvoke(payload)
                    : route.Handler.DynamicInvoke();
                if ((message.ServiceType == ServiceType.RequestResponse || message.ServiceType == ServiceType.Broadcast)
                    && route.Handler.Method.ReturnType != typeof(void))
                {
                    var response = new Message(Format, message.ServiceUri, message.ServiceType, App.InstanceName)
                    {
                        Payload = result,
                        CorrelationId = message.Id,
                        ServiceLevel = message.ServiceLevel,
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

        protected virtual bool IsDelegatedRequest(object payload, Type payloadType, out Type requestType)
        {
            requestType = payloadType;

            if (requestType.IsTypeOrSubtypeOf<NominateExecutionRequest>())
            {
                if (((NominateExecutionRequest)payload).IsPayloadDeferred)
                {
                    requestType = TypeHelper.GetType(((NominateExecutionRequest)payload).RequestType);
                }
                else
                {
                    requestType = ((NominateExecutionRequest)payload).Request.GetType();
                }
                if (requestType.Implements(typeof(ISerializer<>)))
                {
                    requestType = requestType.BaseType;
                }
                return true;
            }

            return false;
        }

        protected virtual void HandleCallback(MessageReceivedHandler callback, Message message)
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
                    lock (_receivers)
                    {
                        // we only care about one response, so remove the callback
                        _receivers.Remove(message.CorrelationId);
                    }
                }
            }
        }

        protected virtual void OnSocketException(Exception e)
        {
            Logger.LogError(e);

            if (this.SocketException != null)
            {
                this.SocketException(this, new SocketExceptionEventArgs(this, e));
            }
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
