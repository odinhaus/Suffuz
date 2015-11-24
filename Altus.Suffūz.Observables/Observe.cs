using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz
{
    public class Observe<T>
    {
        public static Observe<T> From(string channelId)
        {
            return new Observe<T>() { _channelId = channelId };
        }

        public Observable<T> As(string globalObjectKey)
        {
            return new Observable<T>(globalObjectKey);
        }

        public Observable<T> As(Func<T> creator)
        {
            return new Observable<T>(Guid.NewGuid().ToString());
        }

        public Observable<T> As(Func<T> creator, string globalKey)
        {
            return new Observable<T>(globalKey);
        }

        private string _channelId;

    }
}
