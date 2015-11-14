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
        public static TResponse Execute<TRequest, TResponse>(this Op<TRequest, TResponse> request, int timeout = -1)
        {
            var channelService = App.Resolve<IChannelService>();
            var channel = channelService.Create(request.ChannelName);
            return channel.Call<TRequest, TResponse>(
                new ChannelRequest<TRequest, TResponse>(request.ChannelName)
                {
                    Timeout = timeout > 0 ? TimeSpan.FromMilliseconds(timeout) : TimeSpan.FromMilliseconds(30000),
                    Payload = request.Request
                });
        }

        public static AggregateExecutor<TRequest, TResponse> Aggregate<TRequest, TResponse>(this Op<TRequest, TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator)
        {
            return new Extensions.AggregateExecutor<TRequest, TResponse>(request, aggregator, (responses) => false);
        }

        public static DelegateExecutor<TRequest, TResponse> Delegate<TRequest, TResponse>(this Op<TRequest, TResponse> request, Expression<Func<CapacityResponse, bool>> delegator)
        {
            return new Extensions.DelegateExecutor<TRequest, TResponse>(request, delegator);
        }

        public class AggregateExecutor<TRequest, TResponse>
        {
            private Func<IEnumerable<TResponse>, IEnumerable<TResponse>> _aggregator;
            private Op<TRequest, TResponse> _request;
            private Expression<Func<CapacityResponse, bool>> _delegator;
            private Func<IEnumerable<TResponse>, bool> _terminator;

            public AggregateExecutor(Op<TRequest, TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator, Func<IEnumerable<TResponse>, bool> terminator)
            {
                this._aggregator = aggregator;
                this._request = request;
                this._terminator = terminator;
            }

            public AggregateExecutor(Op<TRequest, TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator, Func<IEnumerable<TResponse>, bool> terminator, Expression<Func<CapacityResponse, bool>> delegator)
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
                        Timeout = timeout >= 0 ? TimeSpan.FromMilliseconds(timeout) : TimeSpan.FromMilliseconds(5000)
                    };
                    var enumerableResponse = new EnumerableResponse<TResponse>(request.Timeout, _terminator);
                    Task.Run(() => channel.Call(request, enumerableResponse.Predicate)); // we don't want to block here
                    return _aggregator(enumerableResponse);
                }
                else
                {
                    var request = new ChannelRequest<DelegatedExecutionRequest, TResponse>(_request.ChannelName)
                    {
                        Timeout = timeout >= 0 ? TimeSpan.FromMilliseconds(timeout) : TimeSpan.FromMilliseconds(5000),
                        Payload = new DelegatedExecutionRequest()
                        {
                            Request = _request.Request,
                            Delegator = new Serialization.Expressions.ExpressionSerializer().Serialize(_delegator).ToString()
                        }
                    };
                    var enumerableResponse = new EnumerableResponse<TResponse>(request.Timeout, _terminator);
                    Task.Run(() => channel.Call(request, enumerableResponse.Predicate)); // we don't want to block here
                    return _aggregator(enumerableResponse);
                }
            }
        }

        public class DelegateExecutor<TRequest, TResponse>
        {
            private Expression<Func<CapacityResponse, bool>> _delegator;
            private Op<TRequest, TResponse> _request;

            public DelegateExecutor(Op<TRequest, TResponse> request, Expression<Func<CapacityResponse, bool>> delegator)
            {
                this._delegator = delegator;
                this._request = request;
            }

            public TResponse Execute(int timeout = -1)
            {
                var channelService = App.Resolve<IChannelService>();
                var channel = channelService.Create(_request.ChannelName);
                return channel.Call<DelegatedExecutionRequest, TResponse>(
                new ChannelRequest<DelegatedExecutionRequest, TResponse>(_request.ChannelName)
                {
                    Timeout = timeout >= 0 ? TimeSpan.FromMilliseconds(timeout) : TimeSpan.FromMilliseconds(30000),
                    Payload = new DelegatedExecutionRequest()
                    {
                        Request = _request.Request,
                        Delegator = new Serialization.Expressions.ExpressionSerializer().Serialize(_delegator).ToString()
                    }
                });
            }

            public AggregateExecutor<TRequest, TResponse> Aggregate(Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator)
            {
                return new AggregateExecutor<TRequest, TResponse>(this._request, aggregator, (responses) => false,  _delegator);
            }

            public AggregateExecutor<TRequest, TResponse> Aggregate(Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator, Func<IEnumerable<TResponse>, bool> terminator)
            {
                return new AggregateExecutor<TRequest, TResponse>(this._request, aggregator, terminator, _delegator);
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
