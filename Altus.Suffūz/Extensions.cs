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

namespace Altus.Suffūz
{
    public static class Extensions
    {
        public static TResponse Execute<TRequest, TResponse>(this Get<TRequest, TResponse> request, int timeout = -1, params string[] recipients)
        {
            var channelService = App.ResolveAll<IChannelService>().First(c => c.CanCreate(request.ChannelName));
            var channel = channelService.Create(request.ChannelName);
            if (recipients == null || recipients.Length == 0)
            {
                recipients = new string[] { "*" };
            }

            return channel.Call<TRequest, TResponse>(
                new ChannelRequest<TRequest, TResponse>(request.ChannelName)
                {
                    Timeout = timeout > 0 ? TimeSpan.FromMilliseconds(timeout) : request.TimeOut,
                    Payload = request.Request,
                    Recipients = recipients
                });
        }

       

        public static EnumerableExecutor<TRequest, TResponse> Enumerate<TRequest, TResponse>(this Get<TRequest, TResponse> request)
        {
            return new Extensions.EnumerableExecutor<TRequest, TResponse>(request, (responses) => responses, (responses) => false);
        }

        public static EnumerableExecutor<TRequest, TResponse> Enumerate<TRequest, TResponse>(this Get<TRequest, TResponse> request, 
            Func<IEnumerable<TResponse>, bool> terminator)
        {
            return new Extensions.EnumerableExecutor<TRequest, TResponse>(request, (responses) => responses, terminator);
        }

        public static EnumerableExecutor<TRequest, TResponse> Enumerate<TRequest, TResponse>(this Get<TRequest, TResponse> request, 
            Func<IEnumerable<TResponse>, IEnumerable<TResponse>> selector)
        {
            return new Extensions.EnumerableExecutor<TRequest, TResponse>(request, selector, (responses) => false);
        }

        public static EnumerableExecutor<TRequest, TResponse> Enumerate<TRequest, TResponse>(this Get<TRequest, TResponse> request, 
            Func<IEnumerable<TResponse>, IEnumerable<TResponse>> selector, 
            Func<IEnumerable<TResponse>, bool> terminator)
        {
            return new Extensions.EnumerableExecutor<TRequest, TResponse>(request, selector, terminator);
        }


        public static NominateExecutor<TRequest, TResponse> Nominate<TRequest, TResponse>(this Get<TRequest, TResponse> request, 
            Expression<Func<NominateResponse, bool>> nominator)
        {
            return new Extensions.NominateExecutor<TRequest, TResponse>(request, nominator);
        }

        public class EnumerableExecutor<TRequest, TResponse>
        {
            private Func<IEnumerable<TResponse>, IEnumerable<TResponse>> _selector;
            private Get<TRequest, TResponse> _request;
            private Expression<Func<NominateResponse, bool>> _delegator;
            private Func<IEnumerable<TResponse>, bool> _terminator;

            public EnumerableExecutor(Get<TRequest, TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> selector, Func<IEnumerable<TResponse>, bool> terminator)
            {
                this._selector = selector;
                this._request = request;
                this._terminator = terminator;
            }

            public EnumerableExecutor(Get<TRequest, TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> selector, Func<IEnumerable<TResponse>, bool> terminator, Expression<Func<NominateResponse, bool>> delegator)
                : this(request, selector, terminator)
            {
                this._delegator = delegator;
            }

            public IEnumerable<TResponse> Execute(int timeout = -1, params string[] recipients)
            {
                var channelService = App.ResolveAll<IChannelService>().First(c => c.CanCreate(_request.ChannelName));
                var channel = channelService.Create(_request.ChannelName);
                if (recipients == null || recipients.Length == 0)
                {
                    recipients = new string[] { "*" };
                }
                if (_delegator == null)
                {
                    var request = new ChannelRequest<TRequest, TResponse>(_request.ChannelName)
                    {
                        Payload = _request.Request,
                        Timeout = timeout >= 0 ? TimeSpan.FromMilliseconds(timeout) : _request.TimeOut,
                        Recipients = recipients
                    };
                    var enumerableResponse = new EnumerableResponse<TResponse>(request.Timeout, _terminator);
                    Task.Run(() => channel.Call(request, enumerableResponse.Predicate)); // we don't want to block here
                    return _selector(enumerableResponse);
                }
                else
                {
                    var request = new ChannelRequest<NominateExecutionRequest, TResponse>(_request.ChannelName)
                    {
                        Timeout = timeout >= 0 ? TimeSpan.FromMilliseconds(timeout) : _request.TimeOut,
                        Payload = new NominateExecutionRequest()
                        {
                            Request = _request.Request,
                            Nominator = new Serialization.Expressions.ExpressionSerializer().Serialize(_delegator).ToString(),
                            ScalarResults = false
                        },
                        Recipients = recipients
                    };
                    var enumerableResponse = new EnumerableResponse<TResponse>(request.Timeout, _terminator);
                    Task.Run(() => channel.Call(request, enumerableResponse.Predicate)); // we don't want to block here
                    return _selector(enumerableResponse);
                }
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
                var channelService = App.ResolveAll<IChannelService>().First(c => c.CanCreate(_request.ChannelName));
                var channel = channelService.Create(_request.ChannelName);
                if (recipients == null || recipients.Length == 0)
                {
                    recipients = new string[] { "*" };
                }
                return channel.Call<NominateExecutionRequest, TResponse>(
                new ChannelRequest<NominateExecutionRequest, TResponse>(_request.ChannelName)
                {
                    Timeout = timeout >= 0 ? TimeSpan.FromMilliseconds(timeout) : _request.TimeOut,
                    Payload = new NominateExecutionRequest()
                    {
                        Request = _request.Request,
                        Nominator = new Serialization.Expressions.ExpressionSerializer().Serialize(_nominator).ToString(),
                        ScalarResults = true
                    },
                    Recipients = recipients
                });
            }

            public EnumerableExecutor<TRequest, TResponse> Enumerate()
            {
                return new EnumerableExecutor<TRequest, TResponse>(this._request, (responses) => responses, (responses) => false, _nominator);
            }

            public EnumerableExecutor<TRequest, TResponse> Enumerate(Func<IEnumerable<TResponse>, IEnumerable<TResponse>> selector)
            {
                return new EnumerableExecutor<TRequest, TResponse>(this._request, selector, (responses) => false,  _nominator);
            }

            public EnumerableExecutor<TRequest, TResponse> Enumerate(Func<IEnumerable<TResponse>, IEnumerable<TResponse>> selector, Func<IEnumerable<TResponse>, bool> terminator)
            {
                return new EnumerableExecutor<TRequest, TResponse>(this._request, selector, terminator, _nominator);
            }
        }

        public class EnumerableResponse<TResponse> : IEnumerable<TResponse>
        {
            ManualResetEventSlim _evt = new ManualResetEventSlim(false);
            ConcurrentQueue<TResponse> _queue = new ConcurrentQueue<TResponse>();

            public EnumerableResponse(TimeSpan timeout, Func<IEnumerable<TResponse>, bool> terminator)
            {
                IsComplete = false;
                Predicate = (response) =>
                {
                    if (!IsComplete)
                    {
                        lock (_queue)
                        {
                            _queue.Enqueue(response);
                            _evt.Set();
                        }
                    }
                    return IsComplete;
                };
                Timeout = timeout;
                Terminator = terminator;
            }

            public EnumerableResponse(TimeSpan timeout, Func<TResponse, bool> predicate, Func<IEnumerable<TResponse>, bool> terminator)
            {
                // keep adding until predicate returns true
                IsComplete = false;
                Predicate = (response) =>
                {
                    if (!IsComplete)
                    {
                        IsComplete = !predicate(response);
                        if (!IsComplete)
                        {
                            lock (_queue)
                            {
                                _queue.Enqueue(response);
                                _evt.Set();
                            }
                        }
                    }
                    return IsComplete;
                };
                Timeout = timeout;
                Terminator = terminator;
            }

            public Func<TResponse, bool> Predicate { get; private set; }
            public Func<IEnumerable<TResponse>, bool> Terminator { get; private set; }
            public bool IsComplete { get; private set; }
            public TimeSpan Timeout { get; private set; }

            List<TResponse> _responses = new List<TResponse>();
            public IEnumerator<TResponse> GetEnumerator()
            {
                var startTime = CurrentTime.Now;
                var totalTimeToWait = Timeout.TotalMilliseconds;
                var timeToWait = totalTimeToWait;
                while(!IsComplete)
                {
                    var currentTime = CurrentTime.Now;
                    timeToWait -= currentTime.Subtract(startTime).TotalMilliseconds;
                    startTime = currentTime;

                    if (_evt.Wait((int)timeToWait))
                    {
                        lock(_queue)
                        {
                            TResponse response;
                            while(_queue.TryDequeue(out response) && !IsComplete)
                            {
                                yield return response;
                                _responses.Add(response);
                                IsComplete = Terminator(_responses.ToArray());
                            }
                            _evt.Reset();
                        }
                    }
                    else
                    {
                        IsComplete = true;
                    }
                }
                _responses.Clear();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
