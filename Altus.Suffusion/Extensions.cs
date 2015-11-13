using Altus.Suffusion.Messages;
using Altus.Suffusion.Protocols;
using Altus.Suffusion.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion
{
    public static class Extensions
    {
        //public  static Task<TResponse> Execute<TRequest, TResponse>(this TRequest request, int timeout = -1)
        //{
        //    throw new NotImplementedException();
        //}

        //public  static Task Execute<TRequest, TResponse>(this TRequest request, Action<IEnumerable<TResponse>> aggregator, int timeout = -1)
        //{
        //    throw new NotImplementedException();
        //}

        //public  static Task<TResponse> Execute<TRequest, TResponse>(this TRequest request, Func<CapacityResponse<TRequest>, bool> capacityPredicate, int timeout = -1)
        //{
        //    throw new NotImplementedException();
        //}

        public static TResponse Execute<TResponse>(this Op<NoArgs,TResponse> request, int timeout = -1)
        {
            return Execute(request, timeout);
        }

        public static TResponse Execute<TRequest, TResponse>(this Op<TRequest, TResponse> request, int timeout = -1)
        {
            var channelService = App.Resolve<IChannelService>();
            var channel = channelService.Create(request.ChannelName);
            return channel.Call<TRequest, TResponse>(
                new ChannelRequest<TRequest>(request.ChannelName)
                {
                    Timeout = timeout > 0 ? TimeSpan.FromMilliseconds(timeout) : TimeSpan.FromMilliseconds(30000),
                    Payload = request.Request
                });
        }

        public static Task<IEnumerable<TResponse>> Execute<TResponse>(this Op<NoArgs, TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator, int timeout = -1)
        {
            return Execute(request, aggregator, timeout);
        }

        public static Task<IEnumerable<TResponse>> Execute<TRequest, TResponse>(this Op<TRequest, TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator, int timeout = -1)
        {
            throw new NotImplementedException();
        }

        public static Task<TResponse> Execute<TResponse>(this Op<NoArgs, TResponse> request, Func<CapacityResponse, bool> capacityPredicate, int timeout = -1)
        {
            return Execute(request, capacityPredicate, timeout);
        }

        public static Task<TResponse> Execute<TRequest, TResponse>(this Op<TRequest, TResponse> request, Func<CapacityResponse, bool> capacityPredicate, int timeout = -1)
        {
            throw new NotImplementedException();
        }

        public static Task<TResponse> Execute<TResponse>(this Op<NoArgs, TResponse> request, Func<IEnumerable<CapacityResponse>, bool> capacityPredicate, int timeout = -1)
        {
            return Execute(request, capacityPredicate, timeout);
        }

        public static Task<TResponse> Execute<TRequest, TResponse>(this Op<TRequest, TResponse> request, Func<IEnumerable<CapacityResponse>, bool> capacityPredicate, int timeout = -1)
        {
            throw new NotImplementedException();
        }

        public static AggregateExecutor<NoArgs, TResponse> Aggregate<TResponse>(this Op<NoArgs, TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator)
        {
            return Aggregate(request, aggregator);
        }

        public static AggregateExecutor<TRequest, TResponse> Aggregate<TRequest, TResponse>(this Op<TRequest, TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator)
        {
            return new Extensions.AggregateExecutor<TRequest, TResponse>(request, aggregator);
        }

        public static EnumerableDelegateExecutor<NoArgs, TResponse> Delegate<TResponse>(this Op<NoArgs, TResponse> request, Expression<Func<IEnumerable<CapacityResponse>, IEnumerable<CapacityResponse>>> delegator)
        {
            return Delegate(request, delegator);
        }

        public static EnumerableDelegateExecutor<TRequest, TResponse> Delegate<TRequest, TResponse>(this Op<TRequest, TResponse> request, Expression<Func<IEnumerable<CapacityResponse>, IEnumerable<CapacityResponse>>> delegator)
        {
            return new Extensions.EnumerableDelegateExecutor<TRequest, TResponse>(request, delegator);
        }

        public static ScalarDelegateExecutor<NoArgs, TResponse> Delegate<TResponse>(this Op<NoArgs, TResponse> request, Expression<Func<CapacityResponse, bool>> delegator)
        {
            return Delegate(request, delegator);
        }

        public static ScalarDelegateExecutor<TRequest, TResponse> Delegate<TRequest, TResponse>(this Op<TRequest, TResponse> request, Expression<Func<CapacityResponse, bool>> delegator)
        {
            return new Extensions.ScalarDelegateExecutor<TRequest, TResponse>(request, delegator);
        }

        public class AggregateExecutor<TRequest, TResponse>
        {
            private Func<IEnumerable<TResponse>, IEnumerable<TResponse>> _aggregator;
            private Op<TRequest, TResponse> _request;

            public AggregateExecutor(Op<TRequest, TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator)
            {
                this._aggregator = aggregator;
                this._request = request;
            }

            public  Task<IEnumerable<TResponse>> Execute(int timeout = -1)
            {
                throw new NotImplementedException();
            }
        }

        public class EnumerableDelegateExecutor<TRequest, TResponse>
        {
            private Expression<Func<IEnumerable<CapacityResponse>, IEnumerable<CapacityResponse>>> _delegator;
            private Op<TRequest, TResponse> _request;

            public EnumerableDelegateExecutor(Op<TRequest, TResponse> request, Expression<Func<IEnumerable<CapacityResponse>, IEnumerable<CapacityResponse>>> delegator)
            {
                this._delegator = delegator;
                this._request = request;
            }

            public AggregateExecutor<TRequest, TResponse> Aggregate(Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator)
            {
                return new AggregateExecutor<TRequest, TResponse>(_request, aggregator);
            }

            public  Task<IEnumerable<TResponse>> Execute(int timeout = -1)
            {
                throw new NotImplementedException();
            }
        }

        public class ScalarDelegateExecutor<TRequest, TResponse>
        {
            private Expression<Func<CapacityResponse, bool>> _delegator;
            private Op<TRequest, TResponse> _request;

            public ScalarDelegateExecutor(Op<TRequest, TResponse> request, Expression<Func<CapacityResponse, bool>> delegator)
            {
                this._delegator = delegator;
                this._request = request;
            }

            public TResponse Execute(int timeout = -1)
            {
                var channelService = App.Resolve<IChannelService>();
                var channel = channelService.Create(_request.ChannelName);
                return channel.Call<DelegatedExecutionRequest, TResponse>(
                new ChannelRequest<DelegatedExecutionRequest>(_request.ChannelName)
                {
                    Timeout = timeout > 0 ? TimeSpan.FromMilliseconds(timeout) : TimeSpan.FromMilliseconds(30000),
                    Payload = new DelegatedExecutionRequest()
                    {
                        Request = _request.Request,
                        Delegator = new Serialization.Expressions.ExpressionSerializer().Serialize(_delegator).ToString()
                    }
                });
            }
        }
    }
}
