using Altus.Suffūz.Serialization;
using Altus.Suffūz.Serialization.Binary;
using Altus.Suffūz.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;

namespace Altus.Suffūz.Observables.Serialization.Binary
{
    public class ObservableSerializer : ISerializer
    {
        public bool IsScalar { get { return false; } }
        public ObservableSerializer(IBinarySerializerBuilder builder)
        {
            Builder = builder;
        }

        public int Priority { get { return int.MaxValue; } }

        private IBinarySerializerBuilder Builder;

        public bool SupportsFormat(string format)
        {
            return format.Equals(StandardFormats.BINARY, StringComparison.InvariantCultureIgnoreCase);
        }

        public bool SupportsType(Type type)
        {
            return IsObservable(type);
        }
        static Dictionary<Type, ISerializer> _serializers = new Dictionary<Type, ISerializer>();
        public byte[] Serialize(object source)
        {
            if (Builder == null) throw (new SerializationException("Binary serializer builder could not be found."));

            ISerializer serializer = null;
            Type t = source.GetType();
            try
            {
                serializer = _serializers[t];
            }
            catch
            {
                serializer = Builder.CreateSerializerType(((IObservable)source).Instance.GetType());
                try
                {
                    _serializers.Add(t, serializer);
                }
                catch { }
            }

            if (serializer == null) throw (new SerializationException("Serializer for type\"" + source.GetType().FullName + "\" could not be found supporting the " + StandardFormats.BINARY + " format."));

            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write(((IObservable)source).GlobalKey);
                    bw.Write(serializer.Serialize(((IObservable)source).Instance));
                    return ms.ToArray();
                }
            }
        }

        public object Deserialize(byte[] source, Type targetType)
        {
            ISerializer serializer = Builder.CreateSerializerType(targetType.BaseType);
            if (serializer == null) throw (new SerializationException("Serializer for type\"" + targetType.FullName + "\" could not be found supporting the " + StandardFormats.BINARY + " format."));
            using (var ms = new MemoryStream(source))
            {
                using (var br = new BinaryReader(ms))
                {
                    var globalKey = br.ReadString();
                    var instance = serializer.Deserialize(ms.GetBytes(), targetType.BaseType);
                    var publisher = App.Resolve<IPublisher>();
                    return GetCreator(targetType.BaseType).DynamicInvoke(App.Resolve<IObservableBuilder>(), instance, globalKey, publisher);
                }
            }
                
        }

        static Dictionary<Type, Delegate> _creators = new Dictionary<Type, Delegate>();
        private Delegate GetCreator(Type type)
        {
            Delegate creator;
            lock(_creators)
            {
                if (!_creators.TryGetValue(type, out creator))
                {
                    var builder = Expression.Parameter(typeof(IObservableBuilder));
                    var createGen = typeof(IObservableBuilder).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                                              .Single(mi => mi.IsGenericMethod)
                                                              .MakeGenericMethod(type);
                    var instance = Expression.Parameter(type);
                    var globalKey = Expression.Parameter(typeof(string));
                    var publisher = Expression.Parameter(typeof(IPublisher));
                    var call = Expression.Call(builder, createGen, instance, globalKey, publisher);
                    var del = typeof(Func<,,,,>).MakeGenericType(typeof(IObservableBuilder), type, typeof(string), typeof(IPublisher), type);
                    creator = Expression.Lambda(del, call, builder, instance, globalKey, publisher).Compile();
                    _creators.Add(type, creator);
                }
            }
            return creator;
        }

        public static bool IsObservable(Type type)
        {
            return type.Implements(typeof(IObservable<>)); ;
        }
    }
}
