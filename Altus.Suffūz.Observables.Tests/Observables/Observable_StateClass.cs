using Altus.Suffūz.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables.Tests.Observables
{
    public sealed class Observable_StateClass : StateClass, IObservable<StateClass>
    {
        public Observable_StateClass(IPublisher publisher, StateClass instance, string globalKey) : base()
        {
            SyncLock = new ExclusiveLock(globalKey);
            GlobalKey = globalKey;
            Instance = instance;
            Publisher = publisher;
        }

        public string GlobalKey
        {
            get;
            private set;
        }

        public StateClass Instance
        {
            get;
            private set;
        }

        public ExclusiveLock SyncLock
        {
            get;
            private set;
        }

        public IPublisher Publisher { get; private set; }

        public override int Size
        {
            get
            {
                return base.Size;
            }

            set
            {
                try
                {
                    SyncLock.Enter();

                    if (value == base.Size) return;

                    var beforeChange = new PropertyUpdate<StateClass, int>(this.GlobalKey,
                    OperationState.Before,
                    "Size",
                    typeof(StateClass),
                    this,
                    EventClass.Commutative,
                    EventOrder.Additive,
                    base.Size,
                    value);
                    Publisher.Publish(beforeChange);

                    base.Size = value;

                    var afterChange = new PropertyUpdate<StateClass, int>(this.GlobalKey,
                       OperationState.After,
                       "Size",
                       typeof(StateClass),
                       this,
                       EventClass.Commutative,
                       EventOrder.Additive,
                       base.Size,
                       value);
                    Publisher.Publish(afterChange);
                }
                finally
                {
                    SyncLock.Exit();
                }
            }
        }

        public override double Score
        {
            get
            {
                return base.Score;
            }
            set
            {
                try
                {
                    
                    SyncLock.Enter();

                    if (value == base.Size) return;

                    var beforeChange = new PropertyUpdate<StateClass, double>(this.GlobalKey,
                        OperationState.Before,
                        "Score",
                        typeof(StateClass),
                        this,
                        EventClass.Commutative,
                        EventOrder.Multiplicative,
                        base.Size,
                        value);
                    Publisher.Publish(beforeChange);

                    base.Score = value;

                    var afterChange = new PropertyUpdate<StateClass, double>(this.GlobalKey,
                       OperationState.After,
                       "Score",
                       typeof(StateClass),
                       this,
                       EventClass.Commutative,
                       EventOrder.Multiplicative,
                       base.Size,
                       value);
                    Publisher.Publish(afterChange);
                }
                finally
                {
                    SyncLock.Exit();
                }
            }
        }

        public override int Hello(string message)
        {
            try
            {
                SyncLock.Enter();

                var args = new RuntimeArgument[] { new RuntimeArgument("message", message) };

                var beforeCall = new MethodCall<StateClass, int>(this.GlobalKey,
                    OperationState.Before,
                    "Hello",
                    typeof(StateClass),
                    this,
                    args);
                Publisher.Publish(beforeCall);

                var afterCall = new MethodCall<StateClass, int>(this.GlobalKey,
                    OperationState.Before,
                    "Hello",
                    typeof(StateClass),
                    this,
                    args,
                    base.Hello(message));
                Publisher.Publish(beforeCall);

                return afterCall.ReturnValue;
            }
            finally
            {
                SyncLock.Exit();
            }
        }
    }
}
