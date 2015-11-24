using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Altus.Suffūz.Observables.Tests.Observables;

namespace Altus.Suffūz.Objects.Tests
{
    [TestClass]
    public class MainTests : IObserver<Observing<StateClass>>
    {
        [TestMethod]
        public void CanGetObjectInstance()
        {
            // create a new StateClass on mychannel using the provided ctor, and assign a system-generated unique key
            var stateClass1 = Observe<StateClass>
                                            .From("mychannel")
                                            .As(() => new StateClass() { RgbColor = 1234, Size = 3 });

            // get an existing instance of StateClass from mychannel using the provided key
            // if the instance does not currently exist, the Observed property value returned will be null until 
            // a participant on mychannel creates an instance using the same key
            // subscribe to updates on the instance
            var stateClass2 = Observe<StateClass>
                                            .From("mychannel")
                                            .As(stateClass1.GlobalKey)
                                            .Subscribe(this);

            // create a new instance, or update an existing instance using the provided ctor, and supplied key
            // subscribe to updates on the instance
            var stateClass3 = Observe<StateClass>
                                            .From("mychannel")
                                            .As(() => new StateClass() { RgbColor = 1234, Size = 3 }, "globalkey")
                                            .Subscribe(this);
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(Observing<StateClass> value)
        {
        }
    }
}
