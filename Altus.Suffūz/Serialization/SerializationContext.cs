﻿using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Serialization
{
    public class SerializationContext : ISerializationContext
    {
        [ThreadStatic()]
        private Encoding _encoding;


        public Encoding TextEncoding
        {
            get
            {
                return _encoding;
            }
            set
            {
                _encoding = value;
            }
        }



        static ISerializationContext _ctx = null;
        public static ISerializationContext Instance
        {
            get
            {
                if (_ctx == null)
                {
                    _ctx = App.Resolve<ISerializationContext>();
                }
                return _ctx;
            }
        }

        static Dictionary<string, ISerializer> _serializers = new Dictionary<string, ISerializer>();
        public ISerializer<T> GetSerializer<T>(string format)
        {
            return (ISerializer<T>)GetSerializer(typeof(T), format);
        }

        public static void AddSerializer<T>(string format) where T : ISerializer, new()
        {
            Dictionary<string, ISerializer> dictionary = _serializers;
            lock (dictionary)
            {
                string key = format + "::" + typeof(T).AssemblyQualifiedName;
                if (_serializers.ContainsKey(key))
                {
                    throw new InvalidOperationException("Serializer already exists");
                }
                _serializers.Add(key, Activator.CreateInstance(typeof(T)) as ISerializer);
            }
        }


        public ISerializer GetSerializer(Type type, string format)
        {
            lock (_serializers)
            {
                string key = format + "::" + type.AssemblyQualifiedName;
                if (_serializers.ContainsKey(key))
                {
                    return _serializers[key];
                }
                else
                {
                    ISerializer serializer = App.ResolveAll<ISerializer>()
                        .Union(_serializers.Values)
                        .Where(s => s.SupportsFormat(format) && s.SupportsType(type))
                        .OrderBy(s => s.Priority)
                        .FirstOrDefault();
                    if (serializer == null)
                    {
                        serializer = new ILSerializerBuilder().CreateSerializerType(type);
                    }

                    _serializers.Add(key, serializer);

                    return serializer;
                }
            }
        }

        public static string ToString(byte[] serialized)
        {
            return Instance.TextEncoding.GetString(serialized);
        }

        public static string ToString(object instance, string format)
        {
            ISerializationContext sc = Instance;
            if (sc == null)
                sc = new SerializationContext();
            ISerializer s = sc.GetSerializer(instance.GetType(), format);
            if (s == null)
            {
                return instance.ToString();
            }
            else
            {
                return ToString(s.Serialize(instance));
            }
        }

        public static string ToString<T>(T instance, string format)
        {
            ISerializationContext sc = Instance;
            if (sc == null)
                sc = new SerializationContext();
            ISerializer<T> s = sc.GetSerializer<T>(format);
            if (s == null)
            {
                return instance.ToString();
            }
            else
            {
                return ToString(s.Serialize(instance));
            }
        }

        public void SetSerializer<TObject, TSerializer>(string format) where TSerializer : ISerializer<TObject>, new()
        {
            SetSerializer(typeof(TObject), typeof(TSerializer), format);
        }

        public void SetSerializer(Type objectType, Type serializerType, string format)
        {
            lock(_serializers)
            {
                string key = format + "::" + objectType.AssemblyQualifiedName;
                _serializers[key] = Activator.CreateInstance(serializerType) as ISerializer;
            }
        }
    }
}
