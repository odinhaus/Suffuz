using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Altus.Suffusion.Protocols
{
    public delegate void TimeoutHandler(object sender, TimeoutException ex);
    public delegate void MessageReceivedHandler(object sender, Message message);
    public class AsyncRequest
    {
        public event TimeoutHandler Timeout;

        public AsyncRequest(Message request, TimeSpan timeout)
        {
            this.Request = request;
            this.WaitHandle = new ManualResetEvent(request.ServiceType != ServiceType.RequestResponse);
            this.ResponseCallback = new MessageReceivedHandler(this.ResponseReceived);
            this.TimeSpan = timeout;
            this.Start = CurrentTime.Now;
            this.Id = Guid.NewGuid().ToString();
        }
        private ManualResetEvent WaitHandle;
        private DateTime Start;
        public string Id { get; private set; }
        public Message Request { get; private set; }
        public Message Response { get; private set; }
        public bool CanHaveResponse { get { return (this.Request.ServiceType == ServiceType.RequestResponse && this.TimeSpan.TotalMilliseconds != 0 && !this.Request.IsReponse); } }
        public bool TimedOut { get { return CurrentTime.Now.Subtract(Start) > this.TimeSpan; } }
        public TimeSpan TimeSpan { get; private set; }
        internal MessageReceivedHandler ResponseCallback { get; private set; }

        public Message GetResponse()
        {
            if (this.CanHaveResponse)
            {
                try
                {
                    int wait = (int)Start.Add(TimeSpan).Subtract(CurrentTime.Now).TotalMilliseconds;
                    if (!TimedOut
                        && wait > 0
                        && this.WaitHandle.WaitOne(wait))
                    {
                        this.WaitHandle.Reset();
                    }
                    else
                        throw (new TimeoutException("The response was not received in the specified TimeSpan"));
                }
                catch (TimeoutException ex)
                {
                    if (Timeout != null)
                    {
                        Timeout(this, ex);
                    }
                    else
                        throw (ex);
                }
            }
            return this.Response;
        }

        private void ResponseReceived(object sender, Message message)
        {
            this.Response = message;
            this.WaitHandle.Set();
        }
    }
}
