using Altus.Suffusion.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Routing
{
    public class ServiceRoute
    {
        public Delegate Handler { get; internal set; }
        public bool HasParameters { get; internal set; }
    }

    public class ServiceRoute<THandler, TRequest, TResponse> : ServiceRoute
    {
        public void Selector(Func<TResponse, bool> selector)
        {
            Select = selector;
        }

        public Func<TResponse, bool> Select { get; private set; }

        public void Aggregator(Func<Aggregate<TResponse>, bool> aggregator)
        {
            Aggregate = aggregator;
        }

        public Func<Aggregate<TResponse>, bool> Aggregate { get; private set; }

        public void Capacitor(Func<TRequest, CapacityResponse<TRequest>> capacitor)
        {
            Capacity = capacitor;
        }

        public Func<TRequest, CapacityResponse<TRequest>> Capacity { get; private set; }
    }
}
