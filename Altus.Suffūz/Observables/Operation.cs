using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public enum EventClass
    {
        /// <summary>
        /// Signifies an event that does not affect the persistent state of the system, like a signal
        /// </summary>
        Ephemeral,
        /// <summary>
        /// Signifies an event that can be applied in any order, like addition or multiplication
        /// </summary>
        Commutative,
        /// <summary>
        /// Signifies an event that explicitly replaces an existing value with a new one
        /// </summary>
        Explicit,
        /// <summary>
        /// Signifies an event that takes place in order, like inserting or append to a list
        /// </summary>
        Sequential,
    }

    public enum EventOrder
    {
        NotApplicable = 0,
        Additive = CommutativeEventType.Additive,
        Multiplicative = CommutativeEventType.Multiplicative,
        Temporal = OrderedEventType.Temporal,
        Logical = OrderedEventType.Logical
    }

    public enum OperationState
    {
        Before,
        After
    }

    public enum OperationMode
    {
        MethodCall,
        PropertyCall,
        Created,
        Disposed
    }

    public abstract class Operation
    {
        protected Operation(string globalKey,
            OperationState state,
            OperationMode mode, 
            string memberName,
            Type instanceType,
            object instance,
            EventClass @class,
            EventOrder order)
        {
            this.GlobalKey = globalKey;
            this.OperationState = state;
            this.OperationMode = mode;
            this.MemberName = memberName;
            this.InstanceType = instanceType;
            this.Instance = instance;
            this.EventClass = @class;
            this.EventOrder = order;
        }

        public string GlobalKey { get; private set; }
        public OperationState OperationState { get; private set; }
        public OperationMode OperationMode { get; private set; }
        public string MemberName { get; private set; }
        public Type InstanceType { get; private set; }
        public object Instance { get; private set; }
        public EventClass EventClass { get; private set; }
        public EventOrder EventOrder { get; private set; }
    }

    public abstract class Operation<T> : Operation where T : class, new()
    {
        protected Operation(string globalKey,
            OperationState state,
            OperationMode mode,
            string memberName,
            T instance,
            EventClass @class,
            EventOrder order) 
            :base(globalKey, state, mode, memberName, typeof(T), instance, @class, order)
        {
        }
        public new T Instance { get; internal set; }
        public Subscription<T> Subscription { get; internal set; }
    }

}
