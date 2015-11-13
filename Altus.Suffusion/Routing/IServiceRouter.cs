using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Routing
{
    public interface IServiceRouter
    {
        ServiceRoute Route<THandler, TPayload, TResult>(string uri, Expression<Func<THandler, TPayload, TResult>> handler);
        ServiceRoute Route<THandler, TResult>(string uri, Expression<Func<THandler, TResult>> handler);
        ServiceRoute Route<THandler, TMessage>(string uri, Expression<Action<THandler, TMessage>> handler);

        ServiceRoute GetRoute(string uri, Type requestType);
        ServiceRoute GetRoute<TRequest>(string uri);
    }
}
