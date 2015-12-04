using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols.Udp
{
    public class BestEffortMulticastChannel : MulticastChannel
    {
        public BestEffortMulticastChannel(IPEndPoint mcastGroup, bool listen) 
            : this(mcastGroup, listen, true)
        { }

        public BestEffortMulticastChannel(IPEndPoint mcastGroup, bool listen, bool excludeMessagesFromSelf)
            : this(new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp), mcastGroup, listen, excludeMessagesFromSelf)
        { }

        public BestEffortMulticastChannel(Socket udpSocket, IPEndPoint mcastGroup, bool listen, bool excludeMessagesFromSelf)
            : base(udpSocket, mcastGroup, listen, excludeMessagesFromSelf)
        { }

        public BestEffortMulticastChannel(IPEndPoint mcastGroup, bool listen, DataReceivedHandler handler)
            : this(mcastGroup, listen, true, handler)
        { }

        public BestEffortMulticastChannel(IPEndPoint mcastGroup, bool listen, bool excludeMessagesFromSelf, DataReceivedHandler handler)
            : this(new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp), mcastGroup, listen, excludeMessagesFromSelf, handler)
        { }


        public BestEffortMulticastChannel(Socket udpSocket, IPEndPoint mcastGroup, bool listen, bool excludeMessagesFromSelf, DataReceivedHandler handler)
            : base (udpSocket, mcastGroup, listen, excludeMessagesFromSelf, handler)
        { }



    }
}
