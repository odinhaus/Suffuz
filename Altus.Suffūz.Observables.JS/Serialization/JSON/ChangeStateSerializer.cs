using Altus.Suffūz.Serialization;
using System;
using System.Text;
using System.IO;
using Altus.Suffūz.IO;


namespace Altus.Suffūz.Observables.Serialization.JS
{
    public class ChangeStateSerializer : ISerializer<ChangeState>
    {
        public bool IsScalar
        {
            get
            {
                return false;
            }
        }

        public int Priority
        {
            get
            {
                return 0;
            }
        }

        public ChangeState Deserialize(Stream inputSource)
        {
            return Deserialize(inputSource.GetBytes());
        }

        public ChangeState Deserialize(byte[] source)
        {
            return (ChangeState)Deserialize(source, typeof(ChangeState));
        }

        public object Deserialize(byte[] source, Type targetType)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject(UTF8Encoding.UTF8.GetString(source), targetType);
        }

        public byte[] Serialize(object source)
        {
            return Serialize((ChangeState)source);
        }

        public byte[] Serialize(ChangeState source)
        {
            return UTF8Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(source));
        }

        public void Serialize(ChangeState source, Stream outputStream)
        {
            StreamHelper.Write(outputStream, Serialize(source));
        }

        public bool SupportsFormat(string format)
        {
            return format == StandardFormats.BINARY;
        }

        public bool SupportsType(Type type)
        {
            return type.Equals(typeof(ChangeState)) 
                || (type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(ChangeState<>)));
        }
    }
}
