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
        public static TResponse Execute<TRequest, TResponse>(this Get<TRequest, TResponse> request, int timeout = -1)
        {
            var channelService = App.Resolve<IChannelService>();
            var channel = channelService.Create(request.ChannelName);
            return channel.Call<TRequest, TResponse>(
                new ChannelRequest<TRequest, TResponse>(request.ChannelName)
                {
                    Timeout = timeout > 0 ? TimeSpan.FromMilliseconds(timeout) : request.TimeOut,
                    Payload = request.Request
                });
        }

        public static AggregateExecutor<TRequest, TResponse> Aggregate<TRequest, TResponse>(this Get<TRequest, TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator)
        {
            return new Extensions.AggregateExecutor<TRequest, TResponse>(request, aggregator, (responses) => false);
        }

        public static NominateExecutor<TRequest, TResponse> Nominate<TRequest, TResponse>(this Get<TRequest, TResponse> request, Expression<Func<NominateResponse, bool>> nominator)
        {
            return new Extensions.NominateExecutor<TRequest, TResponse>(request, nominator);
        }

        public class AggregateExecutor<TRequest, TResponse>
        {
            private Func<IEnumerable<TResponse>, IEnumerable<TResponse>> _aggregator;
            private Get<TRequest, TResponse> _request;
            private Expression<Func<NominateResponse, bool>> _delegator;
            private Func<IEnumerable<TResponse>, bool> _terminator;

            public AggregateExecutor(Get<TRequest, TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator, Func<IEnumerable<TResponse>, bool> terminator)
            {
                this._aggregator = aggregator;
                this._request = request;
                this._terminator = terminator;
            }

            public AggregateExecutor(Get<TRequest, TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator, Func<IEnumerable<TResponse>, bool> terminator, Expression<Func<NominateResponse, bool>> delegator)
                : this(request, aggregator, terminator)
            {
                this._delegator = delegator;
            }

            public IEnumerable<TResponse> Execute(int timeout = -1)
            {
                var channelService = App.Resolve<IChannelService>();
                var channel = channelService.Create(_request.ChannelName);
                if (_delegator == null)
                {
                    var request = new ChannelRequest<TRequest, TResponse>(_request.ChannelName)
                    {
                        Payload = _request.Request,
                        Timeout = timeout >= 0 ? TimeSpan.FromMilliseconds(timeout) : _request.TimeOut
                    };
                    var enumerableResponse = new EnumerableResponse<TResponse>(request.Timeout, _terminator);
                    Task.Run(() => channel.Call(request, enumerableResponse.Predicate)); // we don't want to block here
                    return _aggregator(enumerableResponse);
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
                        }
                    };
                    var enumerableResponse = new EnumerableResponse<TResponse>(request.Timeout, _terminator);
                    Task.Run(() => channel.Call(request, enumerableResponse.Predicate)); // we don't want to block here
                    return _aggregator(enumerableResponse);
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

            public TResponse Execute(int timeout = -1)
            {
                var channelService = App.Resolve<IChannelService>();
                var channel = channelService.Create(_request.ChannelName);
                return channel.Call<NominateExecutionRequest, TResponse>(
                new ChannelRequest<NominateExecutionRequest, TResponse>(_request.ChannelName)
                {
                    Timeout = timeout >= 0 ? TimeSpan.FromMilliseconds(timeout) : _request.TimeOut,
                    Payload = new NominateExecutionRequest()
                    {
                        Request = _request.Request,
                        Nominator = new Serialization.Expressions.ExpressionSerializer().Serialize(_nominator).ToString(),
                        ScalarResults = true
                    }
                });
            }

            public AggregateExecutor<TRequest, TResponse> Aggregate(Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator)
            {
                return new AggregateExecutor<TRequest, TResponse>(this._request, aggregator, (responses) => false,  _nominator);
            }

            public AggregateExecutor<TRequest, TResponse> Aggregate(Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator, Func<IEnumerable<TResponse>, bool> terminator)
            {
                return new AggregateExecutor<TRequest, TResponse>(this._request, aggregator, terminator, _nominator);
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
