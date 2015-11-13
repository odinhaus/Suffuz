using Altus.Suffusion.Protocols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Routing
{
    public delegate void TaskRoute<T>(T payload);
    public delegate U TaskRoute<T, U>(T payload);
    public class ServiceRouter : IServiceRouter
    {
        Dictionary<string, ServiceRoute> _routes = new Dictionary<string, ServiceRoute>();

        public ServiceRoute GetRoute(string uri, Type requestType)
        {
            ServiceRoute route;
            if (requestType == typeof(NoArgs)) requestType = null;
            var key = uri + (requestType?.FullName ?? "null");
            lock (_routes)
            {
                if (!_routes.TryGetValue(key, out route))
                {
                    return null;
                }
            }
            return route;
        }

        public ServiceRoute GetRoute<TRequest>(string uri)
        {
            return GetRoute(uri, typeof(TRequest));
        }

        public ServiceRoute Route<THandler, TPayload, TResult>(string uri, Expression<Func<THandler, TPayload, TResult>> handler)
        {
            var route = new ServiceRoute() { Handler = CreateDelegate(handler), HasParameters = true };
            var key = uri + typeof(TPayload).FullName;
            lock (_routes)
            {
                if (!_routes.ContainsKey(key))
                {
                    _routes.Add(key, route);
                    App.Resolve<IChannelService>().Create(uri); // gets the channel up and running
                }
                else
                {
                    _routes[key] = route;
                }
            }
            return route;
        }

        public ServiceRoute Route<THandler, TResult>(string uri, Expression<Func<THandler, TResult>> handler)
        {
            var route = new ServiceRoute() { Handler = CreateDelegate(handler), HasParameters = false };
            var key = uri + "null";
            lock (_routes)
            {
                if (!_routes.ContainsKey(key))
                {
                    _routes.Add(key, route);
                    App.Resolve<IChannelService>().Create(uri); // gets the channel up and running
                }
                else
                {
                    _routes[key] = route;
                }
            }
            return route;
        }

        public ServiceRoute Route<THandler, TMessage>(string uri, Expression<Action<THandler, TMessage>> handler)
        {
            var route = new ServiceRoute() { Handler = CreateDelegate(handler), HasParameters = true };
            var key = uri + typeof(TMessage).FullName;
            lock (_routes)
            {
                if (!_routes.ContainsKey(key))
                {
                    _routes.Add(key, route);
                    App.Resolve<IChannelService>().Create(uri); // gets the channel up and running
                }
                else
                {
                    _routes[key] = route;
                }
            }
            return route;
        }


        private Func<TPayload, TResult> CreateDelegate<THandler, TPayload, TResult>(Expression<Func<THandler, TPayload, TResult>> handler)
        {
            var bodyCall = (MethodCallExpression)((LambdaExpression)handler).Body;
            var handlerType = bodyCall.Object.Type;
            var createInstanceCall = typeof(App).GetMethod("Resolve").MakeGenericMethod(handlerType);
            // creates new target instance from DI Container
            var instance = Expression.Call(null, createInstanceCall);

            var newBodyCall = Expression.Call(instance, bodyCall.Method, bodyCall.Arguments);
            var lambda = Expression.Lambda<Func<TPayload, TResult>>(newBodyCall, bodyCall.Arguments.OfType<ParameterExpression>());
            return lambda.Compile();
        }


        private Func<TResult> CreateDelegate<THandler, TResult>(Expression<Func<THandler, TResult>> handler)
        {
            var bodyCall = (MethodCallExpression)((LambdaExpression)handler).Body;
            var handlerType = bodyCall.Object.Type;
            var createInstanceCall = typeof(App).GetMethod("Resolve").MakeGenericMethod(handlerType);
            // creates new target instance from DI Container
            var instance = Expression.Call(null, createInstanceCall);

            var newBodyCall = Expression.Call(instance, bodyCall.Method, bodyCall.Arguments);
            var lambda = Expression.Lambda<Func<TResult>>(newBodyCall, bodyCall.Arguments.OfType<ParameterExpression>());
            return lambda.Compile();
        }

        private Action<TMessage> CreateDelegate<THandler, TMessage>(Expression<Action<THandler, TMessage>> handler)
        {
            var bodyCall = (MethodCallExpression)((LambdaExpression)handler).Body;
            var handlerType = bodyCall.Object.Type;
            var createInstanceCall = typeof(App).GetMethod("Resolve").MakeGenericMethod(handlerType);
            // creates new target instance from DI Container
            var instance = Expression.Call(null, createInstanceCall);

            var newBodyCall = Expression.Call(instance, bodyCall.Method, bodyCall.Arguments);
            var lambda = Expression.Lambda<Action<TMessage>>(newBodyCall, bodyCall.Arguments.OfType<ParameterExpression>());
            return lambda.Compile();
        }

    }
}
