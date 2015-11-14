using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Routing
{
    public interface IServiceRouter
    {
        ServiceRoute<TRequest, TResult> Route<THandler, TRequest, TResult>(string channelId, Expression<Func<THandler, TRequest, TResult>> handler);
        ServiceRoute<NoArgs, TResult> Route<THandler, TResult>(string channelId, Expression<Func<THandler, TResult>> handler);
        ServiceRoute<TMessage, NoReturn> Route<THandler, TMessage>(string channelId, Expression<Action<THandler, TMessage>> handler);
        ServiceRoute<NoArgs, NoReturn> Route<THandler>(string channelId, Expression<Action<THandler>> handler);

        ServiceRoute GetRoute(string uri, Type requestType, Type responseType);
        ServiceRoute GetRoute<TRequest, TResponse>(string uri);
    }
}
