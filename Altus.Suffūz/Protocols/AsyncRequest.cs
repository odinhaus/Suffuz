using Altus.Suffūz.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols
{
    public delegate void TimeoutHandler(object sender, TimeoutException ex);
    public delegate void MessageReceivedHandler(Message message);
    public class AsyncRequest
    {
        static Dictionary<string, MessageReceivedHandler> _receivers = new Dictionary<string, MessageReceivedHandler>();

        public AsyncRequest(Message request, TimeSpan timeout, Type responseType)
        {
            this.Request = request;
            this.WaitHandle = new ManualResetEvent(request.ServiceType != ServiceType.RequestResponse);
            this.ResponseCallback = new MessageReceivedHandler(this.ResponseReceived);
            this.TimeSpan = timeout;
            this.Start = CurrentTime.Now;
            this.Id = Guid.NewGuid().ToString();
            this.ResponseType = responseType;
            this.IsComplete = new Func<Message, bool>((response) => true);
            this.AddReceiver();
        }

        public AsyncRequest(Message request, TimeSpan timeout, Type responseType, Func<Message, bool> isHandled)
            : this(request, timeout, responseType)
        {
            this.IsComplete = isHandled;
        }

        private void AddReceiver()
        {
            lock(_receivers)
            {
                _receivers.Add(this.Request.Id, this.ResponseCallback);
            }
        }

        private void RemoveReceiver()
        {
            lock(_receivers)
            {
                _receivers.Remove(this.Response.CorrelationId);
            }
        }

        private MessageReceivedHandler CreateHandler(MessageReceivedHandler handler)
        {
            return new MessageReceivedHandler(( t) =>
            {
                handler(t);
                this.ResponseReceived(t);
            });
        }

        private ManualResetEvent WaitHandle;
        private DateTime Start;
        public string Id { get; private set; }
        public Message Request { get; private set; }
        public Message Response { get; private set; }
        public Type ResponseType { get; private set; }
        public bool CanHaveResponse
        {
            get
            {
                return (this.Request.ServiceType == ServiceType.RequestResponse 
                    && this.ResponseType != typeof(NoReturn)
                    && this.TimeSpan.TotalMilliseconds != 0 
                    && !this.Request.IsReponse
                    && !IsCompleted);
            }
        }
        public bool TimedOut { get { return CurrentTime.Now.Subtract(Start) > this.TimeSpan; } }
        public TimeSpan TimeSpan { get; private set; }
        internal MessageReceivedHandler ResponseCallback { get; private set; }
        public Func<Message, bool> IsComplete { get; private set; }
        public bool IsCompleted { get; set; }
        public bool ReceivedResponse { get; private set; }

        public Message GetResponse()
        {
            if (this.CanHaveResponse)
            {
                int wait = (int)Start.Add(TimeSpan).Subtract(CurrentTime.Now).TotalMilliseconds;
                if (!TimedOut
                    && wait > 0
                    && this.WaitHandle.WaitOne(wait))
                {
                    this.WaitHandle.Reset();
                }
                else if (!ReceivedResponse)
                {
                    // if we received at least one response in the time interval, don't toss the exception
                    throw (new TimeoutException("The response was not received in the specified TimeSpan"));
                }
            }
            return this.Response;
        }

        private void ResponseReceived(Message message)
        {
            this.Response = message;
            this.ReceivedResponse = true;
            if (IsComplete(message))
            {
                IsCompleted = true;
                this.RemoveReceiver();
                this.WaitHandle.Set();
            }
        }

        public static bool HandleMessage(Message message)
        {
            MessageReceivedHandler callback;
            bool hasCallback = false;
            lock (_receivers)
            {
                hasCallback = _receivers.TryGetValue(message.CorrelationId, out callback);
            }
            if (hasCallback)
            {
                try
                {
                    callback(message);
                }
                catch { }
            }
            return hasCallback;
        }
    }
}
