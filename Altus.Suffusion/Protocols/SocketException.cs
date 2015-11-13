using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Protocols
{
    public delegate void SocketExceptionHandler(object sender, SocketExceptionEventArgs e);
    public class SocketExceptionEventArgs
    {
        public SocketExceptionEventArgs(object sender, Exception e)
        {
            SocketException = e;
            Sender = sender;
        }

        public Exception SocketException { get; private set; }
        public object Sender { get; set; }
    }
}
