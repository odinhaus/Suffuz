using Altus.Suffūz.Diagnostics;
using Altus.Suffūz.IO;
using Altus.Suffūz.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Altus.Suffūz.Protocols
{
    [System.Serializable]
    public abstract class IdentifiedMessage
    {
        public IdentifiedMessage(string payloadFormat)
        {
            this.Headers = new Dictionary<string, string>();
            this.Id = Guid.NewGuid().ToString();
            this.MessageType = OnGetMessageType();
            this.Timestamp = CurrentTime.Now;
            this.TTL = TimeSpan.FromSeconds(90);
            this.PayloadFormat = payloadFormat;
            this.CorrelationId = string.Empty;
        }
        public IdentifiedMessage(string payloadFormat, string id)
        {
            this.Headers = new Dictionary<string, string>();
            this.Id = id;
            this.CorrelationId = "";
            this.MessageType = OnGetMessageType();
            this.Timestamp = CurrentTime.Now;
            this.TTL = TimeSpan.FromSeconds(90);
            this.PayloadFormat = payloadFormat;
            this.CorrelationId = string.Empty;
        }

        public string Id { get; set; }
        public string Sender { get; set; }
        private string _cid = string.Empty;
        public string CorrelationId { get { return _cid; } set { _cid = value == null ? string.Empty : value; } }
        public DateTime Timestamp { get; set; }
        public TimeSpan TTL { get; set; }
        public byte MessageType { get; set; }
        public string PayloadFormat { get; protected set; }
        public MemoryStream PayloadStream { get; protected set; }
        public ServiceType ServiceType { get; protected set; }
        public string ServiceUri { get; protected set; }
        public Dictionary<string, string> Headers { get; private set; }

        private object _payload;
        public object Payload
        {
            get { return _payload; }
            set
            {
                _payload = value;
                if (value != null)
                {
                    var type = value.GetType();
                    if (type.Implements(typeof(ISerializer<>)))
                    {
                        type = type.GetTypeInfo().ImplementedInterfaces
                            .Where(t => t.GetGenericTypeDefinition() == typeof(ISerializer<>))
                            .Select(t => t.GetGenericArguments()[0])
                            .First();
                    }
                    _payloadType = type.AssemblyQualifiedName;
                }
            }
        }
        //public ServiceParameterCollection Parameters { get; private set; }
        public int StatusCode { get; set; }
        public Action Action { get; protected set; }
        string _payloadType = string.Empty;
        public string PayloadType { get { return _payloadType; } protected set { _payloadType = value; } }
        public bool IsReponse { get; set; }
        public string Encoding
        {
            get
            {
                if (this.Headers.ContainsKey("Content-Type"))
                {
                    string charset = this.Headers["Content-Type"].Split(';')[1].Trim().Split('=')[1].Trim();
                    return charset;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                string ctype = StandardFormats.GetContentType(this.PayloadFormat);
                ctype += "; charset=" + value;
                if (this.Headers.ContainsKey("Content-Type"))
                {
                    this.Headers["Content-Type"] = ctype;
                }
                else
                {
                    this.Headers.Add("Content-Type", ctype);
                }
            }
        }

        protected abstract byte OnGetMessageType();

        public byte[] ToByteArray()
        {
            byte[] body;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter br = new BinaryWriter(ms);
                br.Write(this.Headers.Count);
                foreach (KeyValuePair<string, string> h in this.Headers)
                    br.Write(h.Key + ":" + h.Value);
                br.Write(this.PayloadFormat);
                br.Write(this.PayloadType);
                br.Write(this.Id);
                br.Write(this.CorrelationId);
                br.Write(this.Sender);
                br.Write(this.Timestamp.Ticks);
                br.Write(this.TTL.Ticks);
                br.Write((int)this.ServiceType);
                br.Write(this.ServiceUri);
                br.Write(this.StatusCode);
                MemoryStream pms = new MemoryStream();
                OnSerializePayload(pms);
                OnSerialize(ms);

                br.Write(pms.Length);
                pms.Position = 0;
                StreamHelper.Copy(pms, ms);

                ms.Position = 0;
                body = StreamHelper.GetBytes(ms);
            }
            byte prefix = 1;
            prefix = (byte)(prefix | (MessageType << 1));
            uint originalLength = (uint)body.Length;
            //bool compressed = body.Length > 20000;
            //if (compressed)
            //{
            //    byte[] cBody = ZipBody(body);
            //    if (cBody.Length < originalLength)
            //    {
            //        body = cBody;
            //        prefix = (byte)(1 + (1 << 7));
            //    }
            //}
            byte[] dest = new byte[1 + 4 + body.Length];
            dest[0] = prefix;
            BitConverter.GetBytes(body.Length).CopyTo(dest, 1); // length prefixer
            body.CopyTo(dest, 5);
            return dest;
        }

        protected virtual void OnSerializePayload(Stream outputStream)
        {
            ISerializer serializer = null;
            Type t = this.Payload.GetType();
            serializer = App.Resolve<ISerializationContext>().GetSerializer(t, this.PayloadFormat);

            if (serializer == null)
                Logger.LogError(new SerializationException("Serializer could not be located for payload type: " + this.PayloadType));
            else
            {
                StreamHelper.Copy(serializer.Serialize(this.Payload), outputStream);
            }
        }

        protected virtual void OnDeserializePayload()
        {
            ISerializer serializer = null;
            Type t = TypeHelper.GetType(this.PayloadType);
            serializer = App.Resolve<ISerializationContext>().GetSerializer(t, this.PayloadFormat);

            if (serializer == null)
                Logger.LogError(new SerializationException("Serializer could not be located for payload type: " + this.PayloadType));
            else
            {
                this.Payload = serializer.Deserialize(this.PayloadStream.ToArray(), t);
            }
        }

        protected virtual void OnSerialize(Stream stream)
        {

        }

        public static IdentifiedMessage FromStream(Stream source)
        {
            IdentifiedMessage msg = null;
            byte msgType = (byte)source.ReadByte();
            msgType = (byte)(msgType >> 1);
            if (msgType == 1)
            {
                msg = new Message();
            }
            msg.Deserialize(source);
            return msg;
        }

        public void Deserialize(Stream source)
        {
            source.Position = 5;
            BinaryReader br = new BinaryReader(source);
            int headerCount = br.ReadInt32();
            for (int i = 0; i < headerCount; i++)
            {
                string header = br.ReadString();
                int idx = header.IndexOf(':');
                this.Headers.Add(header.Substring(0, idx), header.Substring(idx));
            }
            this.PayloadFormat = br.ReadString();
            this.PayloadType = br.ReadString();
            this.Id = br.ReadString();
            this.CorrelationId = br.ReadString();
            this.Sender = br.ReadString();
            this.Timestamp = new DateTime().AddTicks(br.ReadInt64());
            this.TTL = TimeSpan.FromTicks(br.ReadInt64());
            this.ServiceType = (ServiceType)br.ReadInt32();
            this.ServiceUri = br.ReadString();
            this.StatusCode = br.ReadInt32();
            OnDeserialize(source);
            long payloadLength = br.ReadInt64();
            MemoryStream ms = new MemoryStream(br.ReadBytes((int)payloadLength));
            ms.Position = 0;
            this.PayloadStream = ms;
            OnDeserializePayload();
        }

        public void FromByteArray(byte[] tcpBytes)
        {
            using (MemoryStream ms = new MemoryStream(tcpBytes))
            {
                this.Deserialize(ms);
            }
        }

        protected virtual void OnDeserialize(Stream source)
        {

        }
    }

    [System.Serializable]
    public class Message : IdentifiedMessage
    {
        static byte[] _netId;
        static string _empty21 = string.Empty.PadRight(21);

        static Message()
        {
            try
            {
                _netId = ASCIIEncoding.ASCII.GetBytes(App.InstanceName);
            }
            catch
            {
                _netId = ASCIIEncoding.ASCII.GetBytes("UNKNOWN");
            }
        }

        internal Message() : this("") { }

        public Message(string payloadFormat) : base(payloadFormat, Guid.NewGuid().ToString())
        {
            Recipients = new string[0];
            Sender = "";
            DeliveryGuaranteed = false;
            ServiceType = ServiceType.Directed;
            ReceivedBy = _empty21;
        }

        public Message(string payloadFormat, string serviceUri, ServiceType type, string sender) : base(payloadFormat)
        {
            ServiceType = type;
            Recipients = new string[0];
            Sender = sender;
            DeliveryGuaranteed = false;
            ReceivedBy = _empty21;
            ServiceUri = serviceUri;
        }

        public Message(string payloadFormat, string id, string serviceUri, ServiceType type, string sender)
            : base(payloadFormat)
        {
            Id = id;
            ServiceType = type;
            Recipients = new string[0];
            Sender = sender;
            DeliveryGuaranteed = false;
            ReceivedBy = _empty21;
            ServiceUri = serviceUri;
        }

        protected override byte OnGetMessageType()
        {
            return 1;
        }

        internal string ReceivedBy { get; set; }
        public string[] Recipients { get; set; }
        public bool DeliveryGuaranteed { get; set; }

        protected override void OnSerialize(Stream stream)
        {
            base.OnSerialize(stream);

            BinaryWriter br = new BinaryWriter(stream);
            br.Write(ReceivedBy);
            br.Write(this.Recipients.Length);
            foreach (string r in Recipients)
            {
                br.Write(r);
            }
            br.Write(Sender);
            br.Write(DeliveryGuaranteed);
        }

        protected override void OnDeserialize(Stream source)
        {
            base.OnDeserialize(source);

            BinaryReader br = new BinaryReader(source);
            this.ReceivedBy = br.ReadString().Trim();
            int rCount = br.ReadInt32();
            string[] recipients = new string[rCount];
            for (int i = 0; i < rCount; i++)
            {
                recipients[i] = br.ReadString();
            }
            this.Recipients = recipients;
            this.Sender = br.ReadString();
            this.DeliveryGuaranteed = br.ReadBoolean();
        }
    }
}
