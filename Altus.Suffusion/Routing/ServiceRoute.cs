using Altus.Suffūz.Messages;
using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Routing
{
    public abstract class ServiceRoute
    {
        protected Func<CapacityResponse> _capacity;
        protected Func<CapacityResponse, TimeSpan> _delay;

        protected ServiceRoute(string channelId)
        {
            _capacity = () => new CapacityResponse() { Minimum = 0, Maximum = 100, Current = 0, Score = 0.0d };
            _delay = (response) => TimeSpan.FromMilliseconds((1d - response.Score) * 2000);
            ChannelId = channelId;
        }

        public Delegate Handler { get; internal set; }
        public bool HasParameters { get; internal set; }
        public string ChannelId { get; private set; }

        internal CapacityResponse Capacity()
        {
            return _capacity();
        }

        internal TimeSpan Delay(CapacityResponse capacity)
        {
            return _delay(capacity);
        }

        public abstract string Key { get; }

        public static string GetKey(string channelId, Type requestType, Type responseType)
        {
            return channelId + requestType.FullName + responseType.FullName; ;
        }

    }

    public class ServiceRoute<TRequest, TResponse> : ServiceRoute
    {
        public ServiceRoute(string channelId) : base(channelId)
        {
            _key = GetKey(channelId, typeof(TRequest), typeof(TResponse));
        }

        string _key;
        public override string Key
        {
            get
            {
                return _key;
            }
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
