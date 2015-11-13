using Altus.Suffusion.Messages;
using Altus.Suffusion.Protocols;
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
        //public async static Task<TResponse> ExecuteAsync<TRequest, TResponse>(this TRequest request, int timeout = -1)
        //{
        //    throw new NotImplementedException();
        //}

        //public async static Task ExecuteAsync<TRequest, TResponse>(this TRequest request, Action<IEnumerable<TResponse>> aggregator, int timeout = -1)
        //{
        //    throw new NotImplementedException();
        //}

        //public async static Task<TResponse> ExecuteAsync<TRequest, TResponse>(this TRequest request, Func<CapacityResponse<TRequest>, bool> capacityPredicate, int timeout = -1)
        //{
        //    throw new NotImplementedException();
        //}

        public async static Task<TResponse> ExecuteAsync<TResponse>(this Op<TResponse> request, int timeout = -1)
        {
            return await ExecuteAsync((Op<NoArgs, TResponse>)request, timeout);
        }

        public async static Task<TResponse> ExecuteAsync<TRequest, TResponse>(this Op<TRequest, TResponse> request, int timeout = -1)
        {
            throw new NotImplementedException();
        }

        public async static Task<IEnumerable<TResponse>> ExecuteAsync<TResponse>(this Op<TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator, int timeout = -1)
        {
            return await ExecuteAsync((Op<NoArgs, TResponse>)request, aggregator, timeout);
        }

        public async static Task<IEnumerable<TResponse>> ExecuteAsync<TRequest, TResponse>(this Op<TRequest, TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator, int timeout = -1)
        {
            throw new NotImplementedException();
        }

        public async static Task<TResponse> ExecuteAsync<TResponse>(this Op<TResponse> request, Func<CapacityResponse<NoArgs>, bool> capacityPredicate, int timeout = -1)
        {
            return await ExecuteAsync((Op<NoArgs, TResponse>)request, capacityPredicate, timeout);
        }

        public async static Task<TResponse> ExecuteAsync<TRequest, TResponse>(this Op<TRequest, TResponse> request, Func<CapacityResponse<NoArgs>, bool> capacityPredicate, int timeout = -1)
        {
            throw new NotImplementedException();
        }

        public async static Task<TResponse> ExecuteAsync<TResponse>(this Op<TResponse> request, Func<IEnumerable<CapacityResponse<NoArgs>>, bool> capacityPredicate, int timeout = -1)
        {
            return await ExecuteAsync((Op<NoArgs, TResponse>)request, capacityPredicate, timeout);
        }

        public async static Task<TResponse> ExecuteAsync<TRequest, TResponse>(this Op<TRequest, TResponse> request, Func<IEnumerable<CapacityResponse<NoArgs>>, bool> capacityPredicate, int timeout = -1)
        {
            throw new NotImplementedException();
        }

        public static AggregateExecutor<NoArgs, TResponse> Aggregate<TResponse>(this Op<TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator)
        {
            return Aggregate((Op<NoArgs, TResponse>)request, aggregator);
        }

        public static AggregateExecutor<TRequest, TResponse> Aggregate<TRequest, TResponse>(this Op<TRequest, TResponse> request, Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator)
        {
            return new Extensions.AggregateExecutor<TRequest, TResponse>(request, aggregator);
        }

        public static EnumerableDelegateExecutor<NoArgs, TResponse> Delegate<TResponse>(this Op<TResponse> request, Expression<Func<IEnumerable<CapacityResponse<TResponse>>, IEnumerable<CapacityResponse<TResponse>>>> delegator)
        {
            return Delegate((Op<NoArgs, TResponse>)request, delegator);
        }

        public static EnumerableDelegateExecutor<TRequest, TResponse> Delegate<TRequest, TResponse>(this Op<TRequest, TResponse> request, Expression<Func<IEnumerable<CapacityResponse<TResponse>>, IEnumerable<CapacityResponse<TResponse>>>> delegator)
        {
            return new Extensions.EnumerableDelegateExecutor<TRequest, TResponse>(request, delegator);
        }

        public static ScalarDelegateExecutor<NoArgs, TResponse> Delegate<TResponse>(this Op<TResponse> request, Expression<Func<CapacityResponse<TResponse>, bool>> delegator)
        {
            return Delegate((Op<NoArgs, TResponse>)request, delegator);
        }

        public static ScalarDelegateExecutor<TRequest, TResponse> Delegate<TRequest, TResponse>(this Op<TRequest, TResponse> request, Expression<Func<CapacityResponse<TResponse>, bool>> delegator)
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

            public async Task<IEnumerable<TResponse>> ExecuteAsync(int timeout = -1)
            {
                throw new NotImplementedException();
            }
        }

        public class EnumerableDelegateExecutor<TRequest, TResponse>
        {
            private Expression<Func<IEnumerable<CapacityResponse<TResponse>>, IEnumerable<CapacityResponse<TResponse>>>> _delegator;
            private Op<TRequest, TResponse> _request;

            public EnumerableDelegateExecutor(Op<TRequest, TResponse> request, Expression<Func<IEnumerable<CapacityResponse<TResponse>>, IEnumerable<CapacityResponse<TResponse>>>> delegator)
            {
                this._delegator = delegator;
                this._request = request;
            }

            public AggregateExecutor<TRequest, TResponse> Aggregate(Func<IEnumerable<TResponse>, IEnumerable<TResponse>> aggregator)
            {
                return new AggregateExecutor<TRequest, TResponse>(_request, aggregator);
            }

            public async Task<IEnumerable<TResponse>> ExecuteAsync(int timeout = -1)
            {
                throw new NotImplementedException();
            }
        }

        public class ScalarDelegateExecutor<TRequest, TResponse>
        {
            private Expression<Func<CapacityResponse<TResponse>, bool>> _delegator;
            private Op<TRequest, TResponse> _request;

            public ScalarDelegateExecutor(Op<TRequest, TResponse> request, Expression<Func<CapacityResponse<TResponse>, bool>> delegator)
            {
                this._delegator = delegator;
                this._request = request;
                var serializer = new Serialization.Expressions.ExpressionSerializer();
                var expressionXml = serializer.Serialize(delegator);
                Console.Write(expressionXml.ToString());
                var expression = serializer.Deserialize(expressionXml);
                Console.WriteLine();
                Console.Write(expression.ToString());
            }

            public async Task<TResponse> ExecuteAsync(int timeout = -1)
            {
                throw new NotImplementedException();
            }
        }
    }
}
