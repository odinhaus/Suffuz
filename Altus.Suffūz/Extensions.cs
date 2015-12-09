using Altus.Suffūz.Messages;
using Altus.Suffūz.Protocols;
using Altus.Suffūz.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Threading;
using System.Collections.Concurrent;
using static Altus.Suffūz.Extensions;

namespace Altus.Suffūz
{
    public static class Extensions
    {
        public static TResponse Execute<TRequest, TResponse>(this Get<TRequest, TResponse> request, int timeout = -1, params string[] recipients)
        {
            var executor = new TerminalExecutor<TRequest, TResponse>(request,
                    (r) => true, // select any
                    (r) => ChannelContext.Current.Count > 0, // we're finished on the first one
                    null, // no rules to nominate
                    true); // scalar
            return executor.Execute(timeout, recipients).FirstOrDefault();
        }

        public static EnumerableExecutor<TRequest, TResponse> All<TRequest, TResponse>(this Get<TRequest, TResponse> request)
        {
            return new Extensions.EnumerableExecutor<TRequest, TResponse>(request, 
                (response) => true, 
                (responses) => true);
        }

        public static EnumerableExecutor<TRequest, TResponse> Take<TRequest, TResponse>(this Get<TRequest, TResponse> request, 
            Func<TResponse, bool> selector)
        {
            return new Extensions.EnumerableExecutor<TRequest, TResponse>(request, 
                selector, (responses) => false);
        }

        public static TerminalExecutor<TRequest, TResponse> Take<TRequest, TResponse>(this Get<TRequest, TResponse> request,
            int countLimit)
        {
            return new Extensions.TerminalExecutor<TRequest, TResponse>(request, 
                (r) => true,
                (r) => ChannelContext.Current.Count > countLimit,
                null,
                false);
        }

        public static NominateExecutor<TRequest, TResponse> Nominate<TRequest, TResponse>(this Get<TRequest, TResponse> request, 
            Expression<Func<NominateResponse, bool>> nominator)
        {
            return new Extensions.NominateExecutor<TRequest, TResponse>(request, nominator);
        }

        public class EnumerableExecutor<TRequest, TResponse>
        {
            private Func<TResponse, bool> _selector;
            private Get<TRequest, TResponse> _request;
            private Expression<Func<NominateResponse, bool>> _nominator;

            public EnumerableExecutor(Get<TRequest, TResponse> request, Func<TResponse, bool> selector)
            {
                this._selector = selector;
                this._request = request;
                this._nominator = null;
            }

            public EnumerableExecutor(Get<TRequest, TResponse> request, Func<TResponse, bool> selector, Expression<Func<NominateResponse, bool>> nominator)
                : this(request, selector)
            {
                this._nominator = nominator;
            }

            public EnumerableExecutor(Get<TRequest, TResponse> request, Func<int> countLimit, Expression<Func<NominateResponse, bool>> nominator)
                : this(request, (r) => ChannelContext.Current.Count < countLimit())
            {
                this._nominator = nominator;
            }

            public TerminalExecutor<TRequest, TResponse> Until(Func<TResponse, bool> terminalPredicate)
            {
                return new TerminalExecutor<TRequest, TResponse>(_request, _selector, terminalPredicate, _nominator, false);
            }

            public IEnumerable<TResponse> Execute(int timeout = -1, params string[] recipients)
            {
                var executor = new TerminalExecutor<TRequest, TResponse>(_request,
                    _selector,
                    (r) => false,
                    _nominator,
                    false);
                return executor.Execute(timeout, recipients);
            }
        }

        public class NominateExecutor<TRequest, TResponse>
        {
            private Expression<Func<NominateResponse, bool>> _nominator;
            private Get<TRequest, TResponse> _request;

            public NominateExecutor(Get<TRequest, TResponse> request, Expression<Func<NominateResponse, bool>> nominator)
            {
                this._nominator = nominator;
                this._request = request;
            }

            public TResponse Execute(int timeout = -1, params string[] recipients)
            {
                var executor = new TerminalExecutor<TRequest, TResponse>(_request, (response) => true, (response) => false, _nominator, true);
                return executor.Execute(timeout, recipients).FirstOrDefault();
            }

            public EnumerableExecutor<TRequest, TResponse> All()
            {
                return new EnumerableExecutor<TRequest, TResponse>(this._request, (response) => true, _nominator);
            }

            public EnumerableExecutor<TRequest, TResponse> Take(Func<TResponse, bool> selector)
            {
                return new EnumerableExecutor<TRequest, TResponse>(this._request, selector, _nominator);
            }

            public TerminalExecutor<TRequest, TResponse> Take(int countLimit)
            {
                return new Extensions.TerminalExecutor<TRequest, TResponse>(this._request,
                    (r) => true,
                    (r) => ChannelContext.Current.Count > countLimit,
                    null,
                    false);
            }
        }

        public class TerminalExecutor<TRequest, TResponse>
        {
            private Func<TResponse, bool> _terminator;
            private Get<TRequest, TResponse> _request;
            private Func<TResponse, bool> _selector;
            private Expression<Func<NominateResponse, bool>> _nominator;
            private bool _isScalar;

            public TerminalExecutor(Get<TRequest, TResponse> request, 
                Func<TResponse, bool> selector, 
                Func<TResponse, bool> terminalPredicate, 
                Expression<Func<NominateResponse, bool>> nominator,
                bool isScalar)
            {
                this._request = request;
                this._selector = selector;
                this._terminator = terminalPredicate;
                this._nominator = nominator;
                this._isScalar = isScalar;
            }

            public IEnumerable<TResponse> Execute(int timeout = -1, params string[] recipients)
            {
                return ChannelContext.Execute(this, timeout, recipients);
            }

            public Get<TRequest, TResponse> Get { get { return _request; } }
            public Func<TResponse, bool> Selector { get { return _selector; } }
            public Func<TResponse, bool> Terminator { get { return _terminator; } }
            public Expression<Func<NominateResponse, bool>> Nominator { get { return _nominator; } }
            public bool IsScalar { get { return _isScalar; } }
        }

        
    }

    public class ChannelContext
    {
        [ThreadStatic]
        static ChannelContext _current;

        public static ChannelContext Current
        {
            get
            {
                return _current;
            }
            internal set
            {
                _current = value;
            }
        }

        public static IEnumerable<TResponse> Execute<TRequest, TResponse>(TerminalExecutor<TRequest, TResponse> terminalExecutor, int timeout, string[] recipients)
        {
            var ctx = new ChannelContext()
            {
                Count = 0
            };
            ctx.Results = new EnumerableResponse<TRequest, TResponse>(ctx, terminalExecutor, timeout, recipients);
            Current = ctx;
            return (IEnumerable<TResponse>)ctx.Results;
        }

        private ChannelContext() { }

        public int Count { get; private set; }
        public IEnumerable Results { get; set; }

        private class EnumerableResponse<TRequest, TResponse> : IEnumerable<TResponse>
        {
            ManualResetEventSlim _evt = new ManualResetEventSlim(false);
            Queue<TResponse> _queue = new Queue<TResponse>();

            TerminalExecutor<TRequest, TResponse> _executor;
            ChannelContext _ctx;

            Func<TResponse, bool> _handleNewMessage;

            public EnumerableResponse(ChannelContext ctx, TerminalExecutor<TRequest, TResponse> terminalExecutor, int timeout, string[] recipients)
            {
                this._ctx = ctx;
                this._executor = terminalExecutor;
                this._handleNewMessage = (response) =>
                {
                    ChannelContext.Current = _ctx;
                    if (CurrentTime.Now < this.EndTime)
                    {
                        if (_executor.Selector(response))
                        {
                            lock (_queue)
                            {
                                _queue.Enqueue(response);
                                _evt.Set();
                                _ctx.Count++;
                            }
                        }
                        if (_executor.Terminator(response)
                            ||
                           (_executor.IsScalar && _ctx.Count >= 1))
                        {
                            IsComplete = true;
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        IsComplete = true;
                        return true;
                    }
                };
               
                this.Timeout = timeout < 0 ? (int)_executor.Get.Timeout.TotalMilliseconds : timeout;
                this.EndTime = CurrentTime.Now.Add(TimeSpan.FromMilliseconds(this.Timeout));
                this.Recipients = recipients;
            }

            public bool IsComplete { get; private set; }
            public bool IsStarted { get; private set; }
            public DateTime EndTime { get; set; }
            public int Timeout { get; private set; }
            public string[] Recipients { get; private set; }


            public IEnumerator<TResponse> GetEnumerator()
            {
                var startTime = CurrentTime.Now;
                var totalTimeToWait = (double)Timeout;
                var timeToWait = totalTimeToWait;

                ExecuteRequest();

                while (!IsComplete)
                {
                    var currentTime = CurrentTime.Now;
                    timeToWait -= currentTime.Subtract(startTime).TotalMilliseconds;
                    startTime = currentTime;

                    if (timeToWait > 0 && _evt.Wait((int)timeToWait))
                    {
                        lock (_queue)
                        {
                            while (_queue.Count > 0)
                            {
                                TResponse response = _queue.Dequeue();
                                yield return response;
                            }
                            _evt.Reset();
                        }
                    }
                    else
                    {
                        IsComplete = true;
                    }
                }

                if (Timeout > 0 
                    && _executor.IsScalar 
                    && ChannelContext.Current.Count == 0)
                {
                    throw new TimeoutException("The operation timed out without receiving any results");
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private void ExecuteRequest()
            {
                if (IsStarted)
                    throw new InvalidOperationException("The response may only be enumerated once.");
                IsStarted = true;

                var channelService = App.ResolveAll<IChannelService>().First(c => c.CanCreate(_executor.Get.ChannelName));
                var channel = channelService.Create(_executor.Get.ChannelName);
                if (Recipients == null || Recipients.Length == 0)
                {
                    Recipients = new string[] { "*" };
                }

                if (_executor.Nominator == null)
                {
                    ExecuteNominated(channel);
                }
                else
                {
                    ExecuteCollective(channel);
                }
            }

            private void ExecuteNominated(IChannel channel)
            {
                var request = new ChannelRequest<TRequest, TResponse>(_executor.Get.ChannelName)
                {
                    Payload = _executor.Get.Request,
                    Timeout = Timeout >= 0 ? TimeSpan.FromMilliseconds(Timeout) : _executor.Get.Timeout,
                    Recipients = Recipients
                };

                ThreadPool.QueueUserWorkItem((state) =>
                {
                    try
                    {
                        channel.Call(request, this._handleNewMessage);
                    }
                    catch { }
                }); // we don't want to block here
            }

            private void ExecuteCollective(IChannel channel)
            {
                var request = new ChannelRequest<NominateExecutionRequest, TResponse>(_executor.Get.ChannelName)
                {
                    Timeout = Timeout >= 0 ? TimeSpan.FromMilliseconds(Timeout) : _executor.Get.Timeout,
                    Payload = new NominateExecutionRequest()
                    {
                        Request = _executor.Get.Request,
                        Nominator = new Serialization.Expressions.ExpressionSerializer().Serialize(_executor.Nominator).ToString(),
                        ScalarResults = _executor.IsScalar
                    },
                    Recipients = Recipients
                };


                ThreadPool.QueueUserWorkItem((state) =>
                {
                    try
                    {
                        channel.Call(request, this._handleNewMessage);
                    }
                    catch { }
                }); // we don't want to block here
            }
        }
    }
}
