using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Protocols
{
    public class CSGOHeaders
    {
        public const string Id = "CSGO-Id";
        public const string CorrelationId = "CSGO-CorrelationId";
        public const string PayloadFormat = "CSGO-PayloadFormat";
        public const string MessageType = "CSGO-MessageType";
        public const string Sender = "CSGO-Sender";
        public const string Recipient = "CSGO-Recipient";
        public const string GuaranteedDelivery = "CSGO-GuaranteedDelivery";
        public const string PayloadType = "CSGO-PayloadType";
        public const string Timestamp = "CSGO-Timestamp";
        public const string ServiceType = "CSGO-ServoiceType";
        public const string ServiceUri = "CSGO-ServiceUri";
        public const string StatusCode = "CSGO-StatusCode";
        public const string TTL = "CSGO-TTL";
        public const string Encoding = "CSGO-Encoding";
        public const string Action = "CSGO-Action";
        public const string All =
            Id + ", " +
            CorrelationId + ", " +
            PayloadFormat + ", " +
            MessageType + ", " +
            Sender + ", " +
            Recipient + ", " +
            GuaranteedDelivery + ", " +
            Timestamp + ", " +
            ServiceType + ", " +
            ServiceUri + ", " +
            StatusCode + ", " +
            TTL + ", " +
            Encoding + ", " +
            Action;
    }
}
