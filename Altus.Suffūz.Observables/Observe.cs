using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz
{
    public class Observe<T> where T : class, new()
    {
        /// <summary>
        /// Gets an observable instance of T, using the default constructor, assigning the provided globalObjectKey identifier to the instance
        /// </summary>
        /// <param name="globalObjectKey">a gloablly unique Id used to identify the instance</param>
        /// <returns></returns>
        public Subscribable<T> As(string globalObjectKey)
        {
            return new Subscribable<T>(globalObjectKey);
        }

        /// <summary>
        /// Gets an observable instance of T, using the default constructor, and assigning a newly generated globally unique identifier to the instance
        /// </summary>
        /// <returns></returns>
        public static Subscribable<T> As()
        {
            return As(() => new T(), Guid.NewGuid().ToString());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="creator"></param>
        /// <returns></returns>
        public static Subscribable<T> As(Func<T> creator)
        {
            return new Subscribable<T>(Guid.NewGuid().ToString());
        }

        public static Subscribable<T> As(Func<T> creator, string globalKey)
        {
            return new Subscribable<T>(globalKey);
        }
    }

    public class Subscribable<T> where T : class, new()
    {
        public Subscribable(string globalKey)
        {
            this.GlobalKey = globalKey;
        }

        /// <summary>
        /// Subscribes to any property update or method call, before they are applied
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public Subscribable<T> BeforeAny(Expression<Action<AnyOperation<T>>> subscriber)
        {
            return null;
        }

        /// <summary>
        /// Subscribes to any property update or method call, after they are applied
        /// </summary>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public Subscribable<T> AfterAny(Expression<Action<AnyOperation<T>>> subscriber)
        {
            return null;
        }

        /// <summary>
        /// Subscribes to a specific method call before it is called
        /// </summary>
        /// <param name="methodCalled">the method to subscribe to</param>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public Subscribable<T> BeforeCalled(Expression<Action<T>> methodCalled, Expression<Action> subscriber)
        {
            return null;
        }

        /// <summary>
        /// Subscribes to a specific method call after it is called
        /// </summary>
        /// <param name="method">the method to subscribe to</param>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public Subscribable<T> AfterCalled(Expression<Action<T>> method, Expression<Action> subscriber)
        {
            return null;
        }

        /// <summary>
        ///  Subscribes to a specific method call before it is called
        /// </summary>
        /// <param name="methodCalled">the method to subscribe to</param>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public Subscribable<T> BeforeCalled(Expression<Action<T>> methodCalled, Expression<Action<MethodCall<T>>> subscriber)
        {
            return null;
        }

        /// <summary>
        /// Subscribes to a specific method call after it is called
        /// </summary>
        /// <param name="method">the method to subscribe to</param>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public Subscribable<T> AfterCalled(Expression<Action<T>> method, Expression<Action<MethodCall<T>>> subscriber)
        {
            return null;
        }

        /// <summary>
        /// Subscribes to a property value change, before the change is applied
        /// </summary>
        /// <typeparam name="U">the property type</typeparam>
        /// <param name="propertyChanged">the property to subscribe to</param>
        /// <param name="subscriber">the handler to call</param>
        /// <returns></returns>
        public Subscribable<T> BeforeChanged<U>(Expression<Func<T, U>> propertyChanged, Expression<Action<PropertyUpdate<T, U>>> subscriber)
        {
            return null;
        }

        /// <summary>
        /// Subscribes to a property value change, after the change is applied
        /// </summary>
        /// <typeparam name="U">the property type</typeparam>
        /// <param name="method">the property to subscribe to</param>
        /// <param name="subscriber">the handle to call</param>
        /// <returns></returns>
        public Subscribable<T> AfterChanged<U>(Expression<Func<T, U>> method, Expression<Action<PropertyUpdate<T, U>>> subscriber)
        {
            return null;
        }

        /// <summary>
        /// Creates the subscription and starts listening for operations.  Disposing of the returned Subscription instance releases the subscription.
        /// </summary>
        /// <returns></returns>
        public Subscription<T> Subscribe()
        {
            return null;
        }

        /// <summary>
        /// Gets the underlying instance being observed.
        /// </summary>
        public T Instance
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the globally unique ID for the observed instance
        /// </summary>
        public string GlobalKey { get; private set; }
    }

    public class Subscription<T> : IDisposable where T : class, new()
    {
        public Subscribable<T> Subscribable { get; set; }
        public T Instance
        {
            get
            {
                return null;
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public enum OperationState
    {
        Before,
        After
    }

    public enum OperationMode
    {
        MethodCall,
        PropertyCall
    }
    public abstract class Operation<T> where T : class, new()
    {
        public Subscription<T> Subscription { get; private set; }
        public OperationState OperationState { get; private set; }
        public OperationMode OperationMode { get; private set; }
        public string MemberName { get; private set; }
    }

    public class PropertyUpdate<T, U> : Operation<T> where T : class, new()
    {
        public U BaseValue { get; set; }
        public U NewValue { get; set; }
    }

    public class MethodCall<T> : Operation<T> where T : class, new()
    {
        public RuntimeArgument[] Arguments { get; private set; }
    }

    public class AnyOperation<T> : Operation<T> where T : class, new()
    {
        public Operation<T> DiscreteOperation { get; private set; }
        public T BaseValue { get; set; }
    }

    public class RuntimeArgument
    {
        public string Name { get; set; }
        public object Value { get; set; }
    }
}
