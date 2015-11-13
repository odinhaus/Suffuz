using Altus.Suffusion.Messages;
using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Routing
{
    public abstract class ServiceRoute
    {
        protected Func<CapacityResponse> _capacity;
        protected Func<CapacityResponse, TimeSpan> _delay;

        protected ServiceRoute()
        {
            _capacity = () => new CapacityResponse() { Minimum = 0, Maximum = 100, Current = 0, Score = 0.0d };
            _delay = (response) => TimeSpan.FromMilliseconds((1d - response.Score) * 2000);
        }

        public Delegate Handler { get; internal set; }
        public bool HasParameters { get; internal set; }

        internal CapacityResponse Capacity()
        {
            return _capacity();
        }

        internal TimeSpan Delay(CapacityResponse capacity)
        {
            return _delay(capacity);
        }
        
    }

    public class ServiceRoute<TRequest, TResponse> : ServiceRoute
    {
        public ServiceRoute()
        {
            
        }

        public ServiceRoute<TRequest, TResponse> Capacity(Expression<Func<CapacityResponse>> capacity)
        {
            _capacity = capacity.Compile();
            return this;
        }

        public ServiceRoute<TRequest, TResponse> Delay(Expression<Func<CapacityResponse, TimeSpan>> delay)
        {
            _delay = delay.Compile();
            return this;
        }
    }
}
