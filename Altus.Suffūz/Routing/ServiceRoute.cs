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
        protected Func<NominateResponse> _nominator;
        protected Func<NominateResponse, TimeSpan> _delay;

        protected ServiceRoute(string channelId)
        {
            _nominator = () => new NominateResponse() { Score = 1d };
            _delay = (response) => TimeSpan.FromMilliseconds((1d - response.Score) * 2000);
            ChannelId = channelId;
        }

        public Delegate Handler { get; internal set; }
        public bool HasParameters { get; internal set; }
        public string ChannelId { get; private set; }

        internal NominateResponse Nominate()
        {
            return _nominator();
        }

        internal TimeSpan Delay(NominateResponse nomination)
        {
            return _delay(nomination);
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

        public ServiceRoute<TRequest, TResponse> Nominate<TNomination>(Func<TNomination> nominator) where TNomination : NominateResponse
        {
            _nominator = nominator;
            return this;
        }

        public ServiceRoute<TRequest, TResponse> Nominate(Func<double> nominator)
        {
            var compiled = nominator;
            _nominator = () => new NominateResponse() { Score = compiled() };
            return this;
        }

        public ServiceRoute<TRequest, TResponse> Delay(Func<NominateResponse, TimeSpan> delay)
        {
            _delay = delay;
            return this;
        }
    }
}
