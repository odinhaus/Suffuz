using Altus.Suffūz.IO;
using Altus.Suffūz.Observables.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Altus.Suffūz.Serialization.Binary
{
    public class ILSerializerBuilder : IBinarySerializerBuilder, IComparer<MemberInfo>
    {
        private const string _prefix = "suff__serializer__";
        private static AssemblyName _asmName = new AssemblyName() { Name = "Altus.Suffūz.Serializers" };
        private static ModuleBuilder _modBuilder;
        private static AssemblyBuilder _asmBuilder;
        private static Dictionary<string, Func<ISerializer>> _typeCache = new Dictionary<string, Func<ISerializer>>();

        static ILSerializerBuilder()
        {
            _asmBuilder = Thread.GetDomain().DefineDynamicAssembly(_asmName, AssemblyBuilderAccess.RunAndSave);
            _modBuilder = _asmBuilder.DefineDynamicModule(_asmName.Name + ".dll", true);
        }

        public ISerializer CreateSerializerType(Type type)
        {
            Func<ISerializer> activator = null;

            var generatedSerializer = type.GetCustomAttribute(typeof(GeneratedSerializerAttribute));
            if (generatedSerializer != null)
            {
                type = ((GeneratedSerializerAttribute)generatedSerializer).WrappedType;
            }

            if (PrimitiveSerializer.IsPrimitive(type))
            {
                return new PrimitiveSerializer();
            }
            else if (IDictionarySerializer.IsDictionaryType(type))
            {
                return new IDictionarySerializer();
            }
            else if (IListSerializer.IsListType(type))
            {
                return new IListSerializer();
            }
            else if (IEnumerableSerializer.IsIEnumerableType(type))
            {
                return new IEnumerableSerializer();
            }
            else if (type == typeof(object))
            {
                return new ObjectSerializer();
            }
            else if (ObservableSerializer.IsObservable(type))
            {
                return new ObservableSerializer(this);
            }

            lock (_typeCache)
            {
                if (!_typeCache.TryGetValue(type.AssemblyQualifiedName, out activator))
                {
                    var serializerType = ImplementSerializerType(type);
                    activator = CreateActivator(serializerType);
                    _typeCache.Add(type.AssemblyQualifiedName, activator);
                }
            }
            return activator();
        }
#if(DEBUG)
        public void SaveAssembly()
        {
            _asmBuilder.Save(_asmName + ".dll");
        }
#endif
        private Type ImplementSerializerType(Type type)
        {
            var interfaceType = typeof(ISerializer<>).MakeGenericType(type);
            var protocolBuffer = typeof(IProtocolBuffer);
            var className = GetTypeName(type);
            var typeBuilder = _modBuilder.DefineType(
                className, 
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable, 
                type, // base type
                new Type[] { interfaceType, protocolBuffer } // interfaces
                );

            // add custom attribute
            var attribCtor = typeof(GeneratedSerializerAttribute).GetConstructors()[0]; // there's only one
            var caBuilder = new CustomAttributeBuilder(attribCtor, new object[] { type });
            typeBuilder.SetCustomAttribute(caBuilder);
            /*

            public interface ISerializer
            {
                bool SupportsFormat(string format);
                bool SupportsType(Type type);
                byte[] Serialize(object source);
                object Deserialize(byte[] source, Type targetType);
                int Priority { get; }
                bool IsScalar { get; }
            }

            public interface ISerializer<T> : ISerializer
            {
                byte[] Serialize(T source);
                void Serialize(T source, Stream outputStream);
                T Deserialize(byte[] source);
                T Deserialize(Stream inputSource);
            }

            */

            ImplementIsScalar(typeBuilder, interfaceType);
            ImplementPriority(typeBuilder, interfaceType);
            ImplementSupportsFormat(typeBuilder, interfaceType);
            ImplementSupportsType(typeBuilder, interfaceType);

            //var serializeType = ImplementSerializeType(typeBuilder, interfaceType);
            //var deserializeType = ImplementDeserializeType(typeBuilder, interfaceType);
            FieldInfo protoBuffField;
            var protoBuff = ImplementProtocolBufferProperty(typeBuilder, interfaceType, out protoBuffField);
            var ctor = ImplementCtor(typeBuilder, interfaceType, protoBuffField);

            var onSerialize = ImplementOnSerialize(typeBuilder, interfaceType, protoBuff);
            var onDeserialize = ImplementOnDeserialize(typeBuilder, interfaceType, ctor, protoBuff);

            ImplementSerialize(typeBuilder, interfaceType, onSerialize);
            ImplementDeserialize(typeBuilder, interfaceType, onDeserialize);
            var serializeGeneric = ImplementSerializeGeneric(typeBuilder, interfaceType, onSerialize);
            var deserializeGeneric = ImplementDeserializeGeneric(typeBuilder, interfaceType, onDeserialize);
            ImplementSerializeGenericStream(typeBuilder, interfaceType, serializeGeneric);
            ImplementDeserializeGenericStream(typeBuilder, interfaceType, deserializeGeneric);

            return typeBuilder.CreateType();
        }

        private PropertyInfo ImplementProtocolBufferProperty(TypeBuilder typeBuilder, Type interfaceType, out FieldInfo protoBuffField)
        {
            var propType = typeof(byte[]);
            var piName = "__ProtocolBuffer";
            protoBuffField = typeBuilder.DefineField("_" + piName.ToLower(), propType, FieldAttributes.Public);

            var property = typeBuilder.DefineProperty(piName,
                PropertyAttributes.HasDefault,
                propType,
                null);

            var getter = typeBuilder.DefineMethod("get_" + piName,
                MethodAttributes.Public
                | MethodAttributes.SpecialName
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Final
                | MethodAttributes.Virtual,
                propType,
                Type.EmptyTypes);

            var getterCode = getter.GetILGenerator();
            getterCode.Emit(OpCodes.Ldarg_0);
            getterCode.Emit(OpCodes.Ldfld, protoBuffField);
            getterCode.Emit(OpCodes.Ret);
            property.SetGetMethod(getter);

            var setter = typeBuilder.DefineMethod("set_" + piName,
                MethodAttributes.Public
                | MethodAttributes.SpecialName
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Final
                | MethodAttributes.Virtual,
                null,
                new[] { propType });

            var setterCode = setter.GetILGenerator();
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Ldarg_1);
            setterCode.Emit(OpCodes.Stfld, protoBuffField);
            setterCode.Emit(OpCodes.Ret);
            property.SetSetMethod(setter);

            return property;
        }

        private string GetTypeName(Type type)
        {
            string name = type.Namespace + "." + _prefix;
            GetTypeName(ref name, type);
            return name;
        }

        private void GetTypeName(ref string name, Type type)
        {
            if (type.IsGenericType)
            {
                var genType = type.GetGenericTypeDefinition().Name.Replace("<", "").Replace(">", "").Replace(",", "").Replace("`","");
                name += genType;
            
                foreach (var t in type.GetGenericArguments())
                {
                    GetTypeName(ref name, t);
                }
            }
            else
            {
                name += type.Name;
            }
        }

        private IEnumerable<MemberInfo> GetSerializableMembers(Type type)
        {
            return type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                       .Where(mi => 
                          ((mi is FieldInfo) 
                       || ((mi is PropertyInfo) && ((PropertyInfo)mi).CanRead && ((PropertyInfo)mi).CanWrite))
                       && mi.GetCustomAttribute<BinarySerializableAttribute>() != null);
        }

        public int Compare(MemberInfo x, MemberInfo y)
        {
            BinarySerializableAttribute xA = ((BinarySerializableAttribute[])x.GetCustomAttributes(typeof(BinarySerializableAttribute), true))[0];
            BinarySerializableAttribute yA = ((BinarySerializableAttribute[])y.GetCustomAttributes(typeof(BinarySerializableAttribute), true))[0];
            return xA.SortOrder.CompareTo(yA.SortOrder);
        }

        private void SerializeMembers(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, Label exit)
        {
            var members = GetSerializableMembers(typeBuilder.BaseType).ToList();
            members.Sort(this);
            foreach(var member in members)
            {
                if (IsValueType(member))
                {
                    SerializeValueType(typeBuilder, interfaceType, methodCode, member);
                }
                else if (IsNullableValueType(member))
                {
                    SerializeNullableValueType(typeBuilder, interfaceType, methodCode, member);
                }
                else
                {
                    var memberType = MemberType(member);
                    if (memberType == typeof(byte[]))
                    {
                        SerializeByteArray(typeBuilder, interfaceType, methodCode, member);
                    }
                    else if (memberType == typeof(char[]))
                    {
                        SerializeCharArray(typeBuilder, interfaceType, methodCode, member);
                    }
                    else if (memberType == typeof(DateTime))
                    {
                        SerializeDateTime(typeBuilder, interfaceType, methodCode, member);
                    }
                    else if (memberType == typeof(string))
                    {
                        SerializeString(typeBuilder, interfaceType, methodCode, member);
                    }
                    else if (memberType == typeof(DateTime?))
                    {
                        SerializeNullableDateTime(typeBuilder, interfaceType, methodCode, member);
                    }
                    else if (memberType.IsArray)
                    {
                        SerializeArray(typeBuilder, interfaceType, methodCode, member);
                    }
                    else
                    {
                        SerializeObject(typeBuilder, interfaceType, methodCode, member);
                    }
                }
            }
        }

        private void SerializeObject(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            var type = MemberType(member);
            var value = methodCode.DeclareLocal(type);
            var hasValue = methodCode.DeclareLocal(typeof(bool));
            var noValue = methodCode.DefineLabel();
            methodCode.Emit(OpCodes.Ldloc_0); // object to read
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Ldfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetGetMethod());
            }
            methodCode.Emit(OpCodes.Stloc, value);
            methodCode.Emit(OpCodes.Ldloc, value);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Cgt_Un);
            methodCode.Emit(OpCodes.Stloc, hasValue);
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc, hasValue);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, hasValue);
            methodCode.Emit(OpCodes.Brfalse, noValue); // the array is null, don't write anything else

            methodCode.Emit(OpCodes.Ldtoken, type);
            methodCode.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Ldloc, value);
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Call, typeof(_BinarySerializer).GetMethod("Serialize", BindingFlags.Public | BindingFlags.Static));

            methodCode.MarkLabel(noValue);
            methodCode.Emit(OpCodes.Nop);
        }

        private void SerializeArray(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            /*

            IL_0016:  ldloc.0
            IL_0017:  callvirt   instance !0[] class 'Altus.Suffūz.Tests'.Array`1<int32>::get_A()
            IL_001c:  stloc.3
            IL_001d:  ldloc.3
            IL_001e:  ldnull
            IL_001f:  cgt.un
            IL_0021:  stloc.s    hasValue
            IL_0023:  ldloc.2
            IL_0024:  ldloc.s    hasValue
            IL_0026:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(bool)
            IL_002b:  nop
            IL_002c:  ldloc.s    hasValue
            IL_002e:  stloc.s    V_5
            IL_0030:  ldloc.s    V_5
            IL_0032:  brfalse.s  IL_006c
            IL_0034:  nop
            IL_0035:  ldloc.3
            IL_0036:  ldlen
            IL_0037:  conv.i4
            IL_0038:  stloc.s    count
            IL_003a:  ldloc.2
            IL_003b:  ldloc.s    count
            IL_003d:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(int32)
            IL_0042:  nop
            IL_0043:  ldc.i4.0
            IL_0044:  stloc.s    i
            IL_0046:  br.s       IL_005f
            IL_0048:  nop
            IL_0049:  ldloc.2
            IL_004a:  ldloc.3
            IL_004b:  ldloc.s    i
            IL_004d:  ldelem.i4
            IL_004e:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(int32)
            IL_0053:  nop
            IL_0054:  nop
            IL_0055:  ldloc.s    i
            IL_0057:  stloc.s    V_8
            IL_0059:  ldloc.s    V_8
            IL_005b:  ldc.i4.1
            IL_005c:  add
            IL_005d:  stloc.s    i
            IL_005f:  ldloc.s    i
            IL_0061:  ldloc.s    count
            IL_0063:  clt
            IL_0065:  stloc.s    V_9
            IL_0067:  ldloc.s    V_9
            IL_0069:  brtrue.s   IL_0048
            IL_006b:  nop


            */

            var type = MemberType(member);
            var elemType = type.GetElementType();
            var value = methodCode.DeclareLocal(type);
            var hasValue = methodCode.DeclareLocal(typeof(bool));
            var noValue = methodCode.DefineLabel();
            var count = methodCode.DeclareLocal(typeof(int));

            methodCode.Emit(OpCodes.Ldloc_0); // object to read
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Ldfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetGetMethod());
            }
            methodCode.Emit(OpCodes.Stloc, value);
            methodCode.Emit(OpCodes.Ldloc, value);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Cgt_Un);
            methodCode.Emit(OpCodes.Stloc, hasValue);
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc, hasValue);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, hasValue);
            methodCode.Emit(OpCodes.Brfalse, noValue); // the array is null, don't write anything else

            // write array length
            methodCode.Emit(OpCodes.Ldloc, value);
            methodCode.Emit(OpCodes.Ldlen);
            methodCode.Emit(OpCodes.Conv_I4); // i think this is redundant?
            methodCode.Emit(OpCodes.Stloc, count);
            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc, count);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(int) }));

            // loop over array elements
            var i = methodCode.DeclareLocal(typeof(int));
            var checkLoop = methodCode.DefineLabel();
            var topOfLoop = methodCode.DefineLabel();
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Stloc, i);
            methodCode.Emit(OpCodes.Br_S, checkLoop);

            methodCode.MarkLabel(topOfLoop);
            methodCode.Emit(OpCodes.Nop);
            
            if (IsValueType(elemType))
            {
                SerializeValueTypeArrayElement(methodCode, value, i, elemType);
            }
            else if (IsNullableValueType(elemType))
            {
                SerializeNullableValueTypeArrayElement(methodCode, value, i, elemType);
            }
            else if (elemType == typeof(string))
            {
                SerializeStringArrayElement(methodCode, value, i, elemType);
            }
            else if (elemType == typeof(DateTime))
            {
                SerializeDateTimeArrayElement(methodCode, value, i, elemType);
            }
            else if (elemType == typeof(DateTime?))
            {
                SerializeNullableDateTimeArrayElement(methodCode, value, i, elemType);
            }
            else
            {
                throw new NotSupportedException();
            }

            // increment i
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Add);
            methodCode.Emit(OpCodes.Stloc, i);
            methodCode.MarkLabel(checkLoop);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc, count);
            methodCode.Emit(OpCodes.Clt);
            methodCode.Emit(OpCodes.Brtrue, topOfLoop);


            methodCode.MarkLabel(noValue);
            methodCode.Emit(OpCodes.Nop);
        }

        private void SerializeNullableDateTimeArrayElement(ILGenerator methodCode, LocalBuilder array, LocalBuilder i, Type elemType)
        {
            var value = methodCode.DeclareLocal(elemType);
            var date = methodCode.DeclareLocal(typeof(DateTime));
            var binaryDate = methodCode.DeclareLocal(typeof(long));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var writeValue = methodCode.DeclareLocal(typeof(bool));
            var dontWrite = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldelem, elemType);
            methodCode.Emit(OpCodes.Stloc, value);
            methodCode.Emit(OpCodes.Ldloca, value);

            methodCode.Emit(OpCodes.Call, elemType.GetProperty("HasValue").GetGetMethod());
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, writeValue);
            methodCode.Emit(OpCodes.Ldloc, writeValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontWrite);

            methodCode.Emit(OpCodes.Ldloca, value);
            methodCode.Emit(OpCodes.Call, elemType.GetProperty("Value").GetGetMethod());
            methodCode.Emit(OpCodes.Stloc, date);
            methodCode.Emit(OpCodes.Ldloca, date);
            methodCode.Emit(OpCodes.Call, typeof(DateTime).GetMethod("ToBinary", BindingFlags.Public | BindingFlags.Instance));
            methodCode.Emit(OpCodes.Stloc, binaryDate);
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc, binaryDate);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(long) }));

            methodCode.MarkLabel(dontWrite);
            methodCode.Emit(OpCodes.Nop);
        }

        private void SerializeNullableValueTypeArrayElement(ILGenerator methodCode, LocalBuilder array, LocalBuilder i, Type elemType)
        {
            var value = methodCode.DeclareLocal(elemType);
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var writeValue = methodCode.DeclareLocal(typeof(bool));
            var dontWrite = methodCode.DefineLabel();
            var valueType = elemType.GetGenericArguments()[0];
            var baseType = valueType;

            if (valueType.IsEnum)
            {
                baseType = valueType.GetFields()[0].FieldType;
            }

            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldelem, elemType);
            methodCode.Emit(OpCodes.Stloc, value);
            methodCode.Emit(OpCodes.Ldloca, value);
            methodCode.Emit(OpCodes.Call, elemType.GetProperty("HasValue").GetGetMethod());
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, writeValue);
            methodCode.Emit(OpCodes.Ldloc, writeValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontWrite);
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloca, value);
            methodCode.Emit(OpCodes.Call, elemType.GetProperty("Value").GetGetMethod());
            if (valueType.IsEnum)
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { baseType }));
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { valueType }));
            }
            methodCode.MarkLabel(dontWrite);
            methodCode.Emit(OpCodes.Nop);
        }

        private void SerializeDateTimeArrayElement(ILGenerator methodCode, LocalBuilder array, LocalBuilder i, Type elemType)
        {
            var value = methodCode.DeclareLocal(typeof(DateTime));
            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldelem, typeof(DateTime));
            methodCode.Emit(OpCodes.Stloc, value);
            methodCode.Emit(OpCodes.Ldloca, value);
            methodCode.Emit(OpCodes.Call, typeof(DateTime).GetMethod("ToBinary", BindingFlags.Public | BindingFlags.Instance));
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(long) }));
        }

        private void SerializeStringArrayElement(ILGenerator methodCode, LocalBuilder value, LocalBuilder i, Type elemType)
        {
            /*

            IL_0049:  ldloc.3
            IL_004a:  ldloc.s    i
            IL_004c:  ldelem.ref
            IL_004d:  stloc.s    'value'
            IL_004f:  ldloc.s    'value'
            IL_0051:  ldnull
            IL_0052:  ceq
            IL_0054:  stloc.s    isNull
            IL_0056:  ldloc.2
            IL_0057:  ldloc.s    isNull
            IL_0059:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(bool)
            IL_005e:  nop
            IL_005f:  ldloc.s    isNull
            IL_0061:  ldc.i4.0
            IL_0062:  ceq
            IL_0064:  stloc.s    V_10
            IL_0066:  ldloc.s    V_10
            IL_0068:  brfalse.s  IL_0077
            IL_006a:  nop
            IL_006b:  ldloc.2
            IL_006c:  ldloc.3
            IL_006d:  ldloc.s    i
            IL_006f:  ldelem.ref
            IL_0070:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(string)


            */
            var elemValue = methodCode.DeclareLocal(elemType);
            var isElemNull = methodCode.DeclareLocal(typeof(bool));

            var nullElement = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc, value);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldelem_Ref);
            
            methodCode.Emit(OpCodes.Stloc, elemValue);
            methodCode.Emit(OpCodes.Ldloc, elemValue);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Ceq);
            
            methodCode.Emit(OpCodes.Stloc, isElemNull);
            methodCode.Emit(OpCodes.Ldloc, isElemNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isElemNull);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Brfalse, nullElement);

            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc, elemValue);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(string) }));

            methodCode.MarkLabel(nullElement);
            methodCode.Emit(OpCodes.Nop);
        }

        private void SerializeValueTypeArrayElement(ILGenerator methodCode, LocalBuilder value, LocalBuilder i, Type elemType)
        {
            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc, value);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldelem, elemType);
            if (elemType.IsEnum)
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { elemType.GetFields()[0].FieldType }));
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { elemType }));
            }
        }

        private void SerializeNullableDateTime(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            var type = MemberType(member);
            var value = methodCode.DeclareLocal(type);
            var date = methodCode.DeclareLocal(type.GetGenericArguments()[0]);
            var binaryDate = methodCode.DeclareLocal(typeof(long));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var writeValue = methodCode.DeclareLocal(typeof(bool));
            var dontWrite = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc_0); // object to read
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Ldfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetGetMethod());
            }
            methodCode.Emit(OpCodes.Stloc, value);
            methodCode.Emit(OpCodes.Ldloca, value);
            methodCode.Emit(OpCodes.Call, type.GetProperty("HasValue").GetGetMethod());
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, writeValue);
            methodCode.Emit(OpCodes.Ldloc, writeValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontWrite);

            methodCode.Emit(OpCodes.Ldloca, value);
            methodCode.Emit(OpCodes.Call, type.GetProperty("Value").GetGetMethod());
            methodCode.Emit(OpCodes.Stloc, date);
            methodCode.Emit(OpCodes.Ldloca, date);
            methodCode.Emit(OpCodes.Call, typeof(DateTime).GetMethod("ToBinary", BindingFlags.Public | BindingFlags.Instance));
            methodCode.Emit(OpCodes.Stloc, binaryDate);
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc, binaryDate);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(long) }));

            methodCode.MarkLabel(dontWrite);
            methodCode.Emit(OpCodes.Nop);
        }

        private void SerializeString(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            /*

            IL_0016:  ldloc.0
            IL_0017:  callvirt   instance string 'Altus.Suffūz.Tests'.SimplePOCO::get_Q()
            IL_001c:  stloc.3
            IL_001d:  ldloc.3
            IL_001e:  ldnull
            IL_001f:  ceq
            IL_0021:  stloc.s    isNull
            IL_0023:  ldloc.2
            IL_0024:  ldloc.s    isNull
            IL_0026:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(bool)
            IL_002b:  nop
            IL_002c:  ldloc.s    isNull
            IL_002e:  ldc.i4.0
            IL_002f:  ceq
            IL_0031:  stloc.s    V_5
            IL_0033:  ldloc.s    V_5
            IL_0035:  brfalse.s  IL_003f
            IL_0037:  nop
            IL_0038:  ldloc.2
            IL_0039:  ldloc.3
            IL_003a:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(string)
            IL_003f:  nop

            */
            var text = methodCode.DeclareLocal(typeof(string));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var writeValue = methodCode.DeclareLocal(typeof(bool));
            var dontWrite = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc_0); // object to read
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Ldfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetGetMethod());
            }
            methodCode.Emit(OpCodes.Stloc, text);
            methodCode.Emit(OpCodes.Ldloc, text);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, writeValue);
            methodCode.Emit(OpCodes.Ldloc, writeValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontWrite);
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc, text);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(string) }));
            methodCode.MarkLabel(dontWrite);
            methodCode.Emit(OpCodes.Nop);
        }

        private void SerializeDateTime(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            var binaryDate = methodCode.DeclareLocal(typeof(long));
            var date = methodCode.DeclareLocal(typeof(DateTime));

            methodCode.Emit(OpCodes.Ldloc_0); // object to read
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Ldfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetGetMethod());
            }
            methodCode.Emit(OpCodes.Stloc, date);
            methodCode.Emit(OpCodes.Ldloca, date);
            methodCode.Emit(OpCodes.Call, typeof(DateTime).GetMethod("ToBinary", BindingFlags.Public | BindingFlags.Instance));
            methodCode.Emit(OpCodes.Stloc, binaryDate);
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc, binaryDate);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(long) }));
        }

        private void SerializeByteArray(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {           
            var array = methodCode.DeclareLocal(typeof(byte[]));
            var arrayLength = methodCode.DeclareLocal(typeof(int));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var writeValue = methodCode.DeclareLocal(typeof(bool));
            var dontWrite = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc_0); // object to read
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Ldfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetGetMethod());
            }
            methodCode.Emit(OpCodes.Stloc, array);
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, writeValue);
            methodCode.Emit(OpCodes.Ldloc, writeValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontWrite);
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Callvirt, typeof(byte[]).GetProperty("Length").GetGetMethod());
            methodCode.Emit(OpCodes.Stloc, arrayLength);
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc, arrayLength);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(int) }));
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { MemberType(member) }));
            methodCode.MarkLabel(dontWrite);
            methodCode.Emit(OpCodes.Nop);
        }

        private void SerializeCharArray(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            var array = methodCode.DeclareLocal(typeof(char[]));
            var arrayLength = methodCode.DeclareLocal(typeof(int));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var writeValue = methodCode.DeclareLocal(typeof(bool));
            var dontWrite = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc_0); // object to read
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Ldfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetGetMethod());
            }
            methodCode.Emit(OpCodes.Stloc, array);
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, writeValue);
            methodCode.Emit(OpCodes.Ldloc, writeValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontWrite);
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Callvirt, typeof(byte[]).GetProperty("Length").GetGetMethod());
            methodCode.Emit(OpCodes.Stloc, arrayLength);
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc, arrayLength);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(int) }));
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { MemberType(member) }));
            methodCode.MarkLabel(dontWrite);
            methodCode.Emit(OpCodes.Nop);
        }

        private void SerializeValueType(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            var type = MemberType(member);
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc_0); // object to read
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Callvirt, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetGetMethod());
            }
            if (type.IsEnum)
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(int) }));
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { type }));
            }
        }

        private void SerializeNullableValueType(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            /*

            IL_0016:  ldloc.0
            IL_0017:  callvirt   instance valuetype [mscorlib]System.Nullable`1<bool> 'Altus.Suffūz.Tests'.SimplePOCO::get_nA()
            IL_001c:  stloc.3
            IL_001d:  ldloca.s   'value'
            IL_001f:  call       instance bool valuetype [mscorlib]System.Nullable`1<bool>::get_HasValue()
            IL_0024:  stloc.s    isNull
            IL_0026:  ldloc.2
            IL_0027:  ldloc.s    isNull
            IL_0029:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(bool)
            IL_002e:  nop
            IL_002f:  ldloc.s    isNull
            IL_0031:  ldc.i4.0
            IL_0032:  ceq
            IL_0034:  stloc.s    V_5
            IL_0036:  ldloc.s    V_5
            IL_0038:  brfalse.s  IL_004a
            IL_003a:  nop
            IL_003b:  ldloc.2
            IL_003c:  ldloca.s   'value'
            IL_003e:  call       instance !0 valuetype [mscorlib]System.Nullable`1<bool>::get_Value()
            IL_0043:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(bool)
            IL_0048:  nop

            */
            var type = MemberType(member);
            var value = methodCode.DeclareLocal(type);
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var writeValue = methodCode.DeclareLocal(typeof(bool));
            var dontWrite = methodCode.DefineLabel();
            var valueType = type.GetGenericArguments()[0];
            var baseType = valueType;

            if (valueType.IsEnum)
            {
                baseType = valueType.GetFields()[0].FieldType;
            }

            methodCode.Emit(OpCodes.Ldloc_0); // object to read
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Ldfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetGetMethod());
            }
            methodCode.Emit(OpCodes.Stloc, value);
            methodCode.Emit(OpCodes.Ldloca, value);
            methodCode.Emit(OpCodes.Call, type.GetProperty("HasValue").GetGetMethod());
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, writeValue);
            methodCode.Emit(OpCodes.Ldloc, writeValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontWrite);
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloca, value);
            methodCode.Emit(OpCodes.Call, type.GetProperty("Value").GetGetMethod());
            if (valueType.IsEnum)
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { baseType }));
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { valueType }));
            }
            methodCode.MarkLabel(dontWrite);
            methodCode.Emit(OpCodes.Nop);
        }

        private void DeserializeMembers(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, Label exit)
        {
            var members = GetSerializableMembers(typeBuilder.BaseType).ToList();
            members.Sort(this);
            foreach (var member in members)
            {
                CheckStreamPosition(methodCode, exit);
                if (IsValueType(member))
                {
                    DeserializeValueType(typeBuilder, interfaceType, methodCode, member);
                }
                else if (IsNullableValueType(member))
                {
                    DeserializeNullableValueType(typeBuilder, interfaceType, methodCode, member);
                }
                else
                {
                    var memberType = MemberType(member);
                    if (memberType == typeof(byte[]))
                    {
                        DeserializeByteArray(typeBuilder, interfaceType, methodCode, member);
                    }
                    else if (memberType == typeof(char[]))
                    {
                        DeserializeCharArray(typeBuilder, interfaceType, methodCode, member);
                    }
                    else if (memberType == typeof(DateTime))
                    {
                        DeserializeDateTime(typeBuilder, interfaceType, methodCode, member);
                    }
                    else if (memberType == typeof(string))
                    {
                        DeserializeString(typeBuilder, interfaceType, methodCode, member);
                    }
                    else if (memberType == typeof(DateTime?))
                    {
                        DeserializeNullableDateTime(typeBuilder, interfaceType, methodCode, member);
                    }
                    else if (memberType.IsArray)
                    {
                        DeserializeArray(typeBuilder, interfaceType, methodCode, member);
                    }
                    else
                    {
                        DeserializeObject(typeBuilder, interfaceType, methodCode, member);
                    }
                }
            }
        }

        private void DeserializeObject(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            var type = MemberType(member);
            var elemType = type.GetElementType();
            var nullValue = methodCode.DefineLabel();
            
            // check if array is null
            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Brfalse, nullValue);

            
            methodCode.Emit(OpCodes.Ldloc_2); // object to write
            methodCode.Emit(OpCodes.Ldtoken, type);
            methodCode.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Call, typeof(_BinarySerializer).GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Castclass, type);
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Stfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetSetMethod());
            }

            methodCode.MarkLabel(nullValue);
            methodCode.Emit(OpCodes.Nop);
        }

        private void DeserializeArray(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            /*

            IL_003b:  ldloc.2
            IL_003c:  callvirt   instance bool [mscorlib]System.IO.BinaryReader::ReadBoolean()
            IL_0041:  stloc.s    V_5
            IL_0043:  ldloc.s    V_5
            IL_0045:  brfalse.s  IL_008b
            IL_0047:  nop
            IL_0048:  ldloc.2
            IL_0049:  callvirt   instance int32 [mscorlib]System.IO.BinaryReader::ReadInt32()
            IL_004e:  stloc.s    count
            IL_0050:  ldloc.s    count
            IL_0052:  newarr     [mscorlib]System.Int32
            IL_0057:  stloc.s    a
            IL_0059:  ldc.i4.0
            IL_005a:  stloc.s    i
            IL_005c:  br.s       IL_0075
            IL_005e:  nop
            IL_005f:  ldloc.s    a
            IL_0061:  ldloc.s    i
            IL_0063:  ldloc.2
            IL_0064:  callvirt   instance int32 [mscorlib]System.IO.BinaryReader::ReadInt32()
            IL_0069:  stelem.i4
            IL_006a:  nop
            IL_006b:  ldloc.s    i
            IL_006d:  stloc.s    V_9
            IL_006f:  ldloc.s    V_9
            IL_0071:  ldc.i4.1
            IL_0072:  add
            IL_0073:  stloc.s    i
            IL_0075:  ldloc.s    i
            IL_0077:  ldloc.s    count
            IL_0079:  clt
            IL_007b:  stloc.s    V_10
            IL_007d:  ldloc.s    V_10
            IL_007f:  brtrue.s   IL_005e
            IL_0081:  ldloc.0
            IL_0082:  ldloc.s    a
            IL_0084:  callvirt   instance void class 'Altus.Suffūz.Tests'.Array`1<int32>::set_A(!0[])
            IL_0089:  nop
            IL_008a:  nop
            IL_008b:  nop

            */
            var type = MemberType(member);
            var elemType = type.GetElementType();
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var count = methodCode.DeclareLocal(typeof(int));
            var array = methodCode.DeclareLocal(type);
            var i = methodCode.DeclareLocal(typeof(int));

            var nullValue = methodCode.DefineLabel();
            var countCheck = methodCode.DefineLabel();
            var loopStart = methodCode.DefineLabel();

            // check if array is null
            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Brfalse, nullValue);

            // loop and set values
            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            methodCode.Emit(OpCodes.Stloc, count);
            methodCode.Emit(OpCodes.Ldloc, count);
            methodCode.Emit(OpCodes.Newarr, elemType);
            methodCode.Emit(OpCodes.Stloc, array);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Stloc, i);
            methodCode.Emit(OpCodes.Br, countCheck);
            methodCode.MarkLabel(loopStart);
            methodCode.Emit(OpCodes.Nop);

            if (IsValueType(elemType))
            {
                DeserializeValueTypeArrayElement(methodCode, array, i, elemType);
            }
            else if (IsNullableValueType(elemType))
            {
                DeserializeNullableValueTypeArrayElement(methodCode, array, i, elemType);
            }
            else if (elemType == typeof(string))
            {
                DeserializeStringArrayElement(methodCode, array, i, elemType);
            }
            else if (elemType == typeof(DateTime))
            {
                DeserializeDateTimeArrayElement(methodCode, array, i, elemType);
            }
            else if (elemType == typeof(DateTime?))
            {
                DeserializeNullableDateTimeArrayElement(methodCode, array, i, elemType);
            }
            else
                throw new NotSupportedException();

            // check iteration count
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Add);
            methodCode.Emit(OpCodes.Stloc, i);
            methodCode.MarkLabel(countCheck);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc, count);
            methodCode.Emit(OpCodes.Clt);
            methodCode.Emit(OpCodes.Brtrue, loopStart);
            // end loop

            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc, array);
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Stfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetSetMethod());
            }

            methodCode.MarkLabel(nullValue);
            methodCode.Emit(OpCodes.Nop);
        }

        private void DeserializeNullableDateTimeArrayElement(ILGenerator methodCode, LocalBuilder array, LocalBuilder i, Type elemType)
        {
            var binaryDate = methodCode.DeclareLocal(typeof(long));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var readValue = methodCode.DeclareLocal(typeof(bool));
            var dontRead = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc_1); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, readValue);
            methodCode.Emit(OpCodes.Ldloc, readValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontRead);

            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc_1); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            methodCode.Emit(OpCodes.Stloc, binaryDate);
            methodCode.Emit(OpCodes.Ldloc, binaryDate); // object to write
            methodCode.Emit(OpCodes.Call, typeof(DateTime).GetMethod("FromBinary", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Newobj, typeof(DateTime?).GetConstructor(new Type[] { typeof(DateTime) }));
            methodCode.Emit(OpCodes.Stelem, elemType);

            methodCode.MarkLabel(dontRead);
            methodCode.Emit(OpCodes.Nop);
        }

        private void DeserializeNullableValueTypeArrayElement(ILGenerator methodCode, LocalBuilder array, LocalBuilder i, Type elemType)
        {
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var readValue = methodCode.DeclareLocal(typeof(bool));
            var dontRead = methodCode.DefineLabel();
            var valueType = elemType.GetGenericArguments()[0];
            var baseType = valueType;

            if (valueType.IsEnum)
            {
                baseType = valueType.GetFields()[0].FieldType;
            }

            methodCode.Emit(OpCodes.Ldloc_1); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, readValue);
            methodCode.Emit(OpCodes.Ldloc, readValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontRead);

            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc_1); // binary reader
            if (elemType == typeof(bool?))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            }
            else if (elemType == typeof(byte?) || baseType == typeof(byte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadByte"));
            }
            else if (elemType == typeof(sbyte?) || baseType == typeof(sbyte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSByte"));
            }
            else if (elemType == typeof(char?) || baseType == typeof(char))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadChar"));
            }
            else if (elemType == typeof(short?) || baseType == typeof(short))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt16"));
            }
            else if (elemType == typeof(ushort?) || baseType == typeof(ushort))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt16"));
            }
            else if (elemType == typeof(int?) || baseType == typeof(int))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            }
            else if (elemType == typeof(uint?) || baseType == typeof(uint))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt32"));
            }
            else if (elemType == typeof(long?) || baseType == typeof(long))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            }
            else if (elemType == typeof(ulong?) || baseType == typeof(ulong))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt64"));
            }
            else if (elemType == typeof(float?))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSingle"));
            }
            else if (elemType == typeof(double?))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadDouble"));
            }
            else if (elemType == typeof(decimal?))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadDecimal"));
            }

            methodCode.Emit(OpCodes.Newobj, elemType.GetConstructor(new Type[] { valueType }));
            methodCode.Emit(OpCodes.Stelem, elemType);

            methodCode.MarkLabel(dontRead);
            methodCode.Emit(OpCodes.Nop);
        }

        private void DeserializeDateTimeArrayElement(ILGenerator methodCode, LocalBuilder array, LocalBuilder i, Type elemType)
        {
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc_1); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            methodCode.Emit(OpCodes.Call, typeof(DateTime).GetMethod("FromBinary", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Stelem, elemType);
        }

        private void DeserializeStringArrayElement(ILGenerator methodCode, LocalBuilder array, LocalBuilder i, Type elemType)
        {
            /*

            IL_005f:  ldloc.2
            IL_0060:  callvirt   instance bool [mscorlib]System.IO.BinaryReader::ReadBoolean()
            IL_0065:  stloc.s    V_9
            IL_0067:  ldloc.s    V_9
            IL_0069:  brfalse.s  IL_0078
            IL_006b:  nop
            IL_006c:  ldloc.s    a
            IL_006e:  ldloc.s    i
            IL_0070:  ldloc.2
            IL_0071:  callvirt   instance string [mscorlib]System.IO.BinaryReader::ReadString()
            IL_0076:  stelem.ref


            */

            var nullElement = methodCode.DefineLabel();
            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Brtrue, nullElement);
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadString"));
            methodCode.Emit(OpCodes.Stelem_Ref);

            methodCode.MarkLabel(nullElement);
            methodCode.Emit(OpCodes.Nop);
        }

        private void DeserializeValueTypeArrayElement(ILGenerator methodCode, LocalBuilder array, LocalBuilder i, Type type)
        {
            var baseType = type;

            if (type.IsEnum)
            {
                baseType = type.GetFields()[0].FieldType;
            }

            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc_1);

            if (type == typeof(bool))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            }
            else if (type == typeof(byte) || baseType == typeof(byte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadByte"));
            }
            else if (type == typeof(sbyte) || baseType == typeof(sbyte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSByte"));
            }
            else if (type == typeof(char) || baseType == typeof(char))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadChar"));
            }
            else if (type == typeof(short) || baseType == typeof(short))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt16"));
            }
            else if (type == typeof(ushort) || baseType == typeof(ushort))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt16"));
            }
            else if (type == typeof(int) || baseType == typeof(int))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            }
            else if (type == typeof(uint) || baseType == typeof(uint))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt32"));
            }
            else if (type == typeof(long) || baseType == typeof(long))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            }
            else if (type == typeof(ulong) || baseType == typeof(ulong))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt64"));
            }
            else if (type == typeof(float))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSingle"));
            }
            else if (type == typeof(double))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadDouble"));
            }
            else if (type == typeof(decimal))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadDecimal"));
            }

            methodCode.Emit(OpCodes.Stelem, type);
        }

        private void DeserializeNullableDateTime(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            var type = MemberType(member);
            var binaryDate = methodCode.DeclareLocal(typeof(long));
            var date = methodCode.DeclareLocal(typeof(DateTime));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var readValue = methodCode.DeclareLocal(typeof(bool));
            var dontRead = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc_1); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, readValue);
            methodCode.Emit(OpCodes.Ldloc, readValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontRead);

            methodCode.Emit(OpCodes.Ldloc_1); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            methodCode.Emit(OpCodes.Stloc, binaryDate);
            methodCode.Emit(OpCodes.Ldloc, binaryDate); // object to write
            methodCode.Emit(OpCodes.Call, typeof(DateTime).GetMethod("FromBinary", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Stloc, date);
            methodCode.Emit(OpCodes.Ldloc_2); // object to write
            methodCode.Emit(OpCodes.Ldloc, date);
            methodCode.Emit(OpCodes.Newobj, typeof(DateTime?).GetConstructor(new Type[] { typeof(DateTime) }));
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Stfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetSetMethod());
            }

            methodCode.MarkLabel(dontRead);
            methodCode.Emit(OpCodes.Nop);
        }

        private void DeserializeString(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            /*

            IL_003e:  callvirt   instance bool [mscorlib]System.IO.BinaryReader::ReadBoolean()
            IL_0043:  stloc.3
            IL_0044:  ldloc.3
            IL_0045:  ldc.i4.0
            IL_0046:  ceq
            IL_0048:  stloc.s    V_6
            IL_004a:  ldloc.s    V_6
            IL_004c:  brfalse.s  IL_006e
            IL_004e:  nop
            IL_004f:  ldloc.0
            IL_0050:  ldloc.2
            IL_0051:  callvirt   instance string [mscorlib]System.IO.BinaryReader::ReadString()
            IL_0056:  callvirt   instance void 'Altus.Suffūz.Tests'.SimplePOCO::set_Q(string)
            IL_006e:  nop

            */

            var text = methodCode.DeclareLocal(typeof(string));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var readValue = methodCode.DeclareLocal(typeof(bool));
            var dontRead = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc_1); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, readValue);
            methodCode.Emit(OpCodes.Ldloc, readValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontRead);
            methodCode.Emit(OpCodes.Ldloc_1); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadString"));
            methodCode.Emit(OpCodes.Stloc, text);
            methodCode.Emit(OpCodes.Ldloc_2); // object to write
            methodCode.Emit(OpCodes.Ldloc, text);
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Stfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetSetMethod());
            }
            methodCode.MarkLabel(dontRead);
            methodCode.Emit(OpCodes.Nop);
        }

        private void DeserializeDateTime(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            /*

            IL_003d:  ldloc.2
            IL_003e:  callvirt   instance int64 [mscorlib]System.IO.BinaryReader::ReadInt64()
            IL_0043:  stloc.3
            IL_0044:  ldloc.3
            IL_0045:  call       valuetype [mscorlib]System.DateTime [mscorlib]System.DateTime::FromBinary(int64)
            IL_004a:  stloc.s    'date'
            IL_004c:  ldloc.0
            IL_004d:  ldloc.s    'date'
            IL_004f:  callvirt   instance void 'Altus.Suffūz.Test'.SimplePOCO::set_P(valuetype [mscorlib]System.DateTime)

            */

            var binaryDate = methodCode.DeclareLocal(typeof(long));
            var date = methodCode.DeclareLocal(typeof(DateTime));

            methodCode.Emit(OpCodes.Ldloc_1); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            methodCode.Emit(OpCodes.Stloc, binaryDate);
            methodCode.Emit(OpCodes.Ldloc, binaryDate); // object to write
            methodCode.Emit(OpCodes.Call, typeof(DateTime).GetMethod("FromBinary", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Stloc, date);
            methodCode.Emit(OpCodes.Ldloc_2); // object to write
            methodCode.Emit(OpCodes.Ldloc, date); 
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Stfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetSetMethod());
            }
        }

        private void DeserializeByteArray(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            var array = methodCode.DeclareLocal(typeof(byte[]));
            var arrayLength = methodCode.DeclareLocal(typeof(int));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var readValue = methodCode.DeclareLocal(typeof(bool));
            var dontRead = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc_1); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, readValue);
            methodCode.Emit(OpCodes.Ldloc, readValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontRead);
            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            methodCode.Emit(OpCodes.Stloc, arrayLength);

            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Ldloc, arrayLength);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBytes"));
            methodCode.Emit(OpCodes.Stloc, array);

            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc, array);
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Stfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetSetMethod());
            }
            methodCode.MarkLabel(dontRead);
            methodCode.Emit(OpCodes.Nop);
        }

        private void DeserializeCharArray(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            var array = methodCode.DeclareLocal(typeof(char[]));
            var arrayLength = methodCode.DeclareLocal(typeof(int));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var readValue = methodCode.DeclareLocal(typeof(bool));
            var dontRead = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc_1); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, readValue);
            methodCode.Emit(OpCodes.Ldloc, readValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontRead);
            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            methodCode.Emit(OpCodes.Stloc, arrayLength);

            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Ldloc, arrayLength);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadChars"));
            methodCode.Emit(OpCodes.Stloc, array);

            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc, array);
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Stfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetSetMethod());
            }
            methodCode.MarkLabel(dontRead);
            methodCode.Emit(OpCodes.Nop);
        }

        private void DeserializeValueType(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            var type = MemberType(member);
            var baseType = type;

            if (type.IsEnum)
            {
                baseType = type.GetFields()[0].FieldType;
            }

            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc_1);
            if (type == typeof(bool))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            }
            else if (type == typeof(byte) || baseType == typeof(byte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadByte"));
            }
            else if (type == typeof(sbyte) || baseType == typeof(sbyte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSByte"));
            }
            else if (type == typeof(char) || baseType == typeof(char))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadChar"));
            }
            else if (type == typeof(short) || baseType == typeof(short))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt16"));
            }
            else if (type == typeof(ushort) || baseType == typeof(ushort))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt16"));
            }
            else if (type == typeof(int) || baseType == typeof(int))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            }
            else if (type == typeof(uint) || baseType == typeof(uint))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt32"));
            }
            else if (type == typeof(long) || baseType == typeof(long))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            }
            else if (type == typeof(ulong) || baseType == typeof(ulong))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt64"));
            }
            else if (type == typeof(float))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSingle"));
            }
            else if (type == typeof(double))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadDouble"));
            }
            else if (type == typeof(decimal))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadDecimal"));
            }
            else if (type.IsEnum)
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            }

            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Stfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetSetMethod());
            }
        }

        private void DeserializeNullableValueType(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            /*

            IL_003d:  ldloc.2
            IL_003e:  callvirt   instance bool [mscorlib]System.IO.BinaryReader::ReadBoolean()
            IL_0043:  stloc.3
            IL_0044:  ldloc.3
            IL_0045:  ldc.i4.0
            IL_0046:  ceq
            IL_0048:  stloc.s    V_6
            IL_004a:  ldloc.s    V_6
            IL_004c:  brfalse.s  IL_0062
            IL_004e:  nop
            IL_004f:  ldloc.0
            IL_0050:  ldloc.2
            IL_0051:  callvirt   instance bool [mscorlib]System.IO.BinaryReader::ReadBoolean()
            IL_0056:  newobj     instance void valuetype [mscorlib]System.Nullable`1<bool>::.ctor(!0)
            IL_005b:  callvirt   instance void 'Altus.Suffūz.Tests'.SimplePOCO::set_nA(valuetype [mscorlib]System.Nullable`1<bool>)
            IL_0060:  nop

            */
            var type = MemberType(member);
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var readValue = methodCode.DeclareLocal(typeof(bool));
            var dontRead = methodCode.DefineLabel();
            var valueType = type.GetGenericArguments()[0];
            var baseType = valueType;

            if (valueType.IsEnum)
            {
                baseType = valueType.GetFields()[0].FieldType;
            }

            methodCode.Emit(OpCodes.Ldloc_1); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, readValue);
            methodCode.Emit(OpCodes.Ldloc, readValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontRead);

            methodCode.Emit(OpCodes.Ldloc_2); // object to write
            methodCode.Emit(OpCodes.Ldloc_1); // binary reader
            if (valueType == typeof(bool))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            }
            else if (valueType == typeof(byte) || valueType == typeof(byte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadByte"));
            }
            else if (valueType == typeof(sbyte) || valueType == typeof(sbyte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSByte"));
            }
            else if (valueType == typeof(char) || valueType == typeof(char))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadChar"));
            }
            else if (valueType == typeof(short) || valueType == typeof(short))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt16"));
            }
            else if (valueType == typeof(ushort) || valueType == typeof(ushort))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt16"));
            }
            else if (valueType == typeof(int) || valueType == typeof(int))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            }
            else if (valueType == typeof(uint) || valueType == typeof(uint))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt32"));
            }
            else if (valueType == typeof(long) || valueType == typeof(long))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            }
            else if (valueType == typeof(ulong))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt64"));
            }
            else if (valueType == typeof(float))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSingle"));
            }
            else if (valueType == typeof(double))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadDouble"));
            }
            else if (valueType == typeof(decimal))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadDecimal"));
            }
            else if (valueType.IsEnum)
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            }
            methodCode.Emit(OpCodes.Newobj, type.GetConstructor(new Type[] { valueType }));

            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Stfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetSetMethod());
            }
            methodCode.MarkLabel(dontRead);
            methodCode.Emit(OpCodes.Nop);
        }

        private void CheckStreamPosition(ILGenerator methodCode, Label exit)
        {
            /*
            C# --------------------------------------------------------------------------------------------------
            if (reader.BaseStream.Position >= reader.BaseStream.Length)
            {
                return serializer;
            }

            IL --------------------------------------------------------------------------------------------------
            IL_0014:  ldloc.1
            IL_0015:  callvirt   instance class [mscorlib]System.IO.Stream [mscorlib]System.IO.BinaryReader::get_BaseStream()
            IL_001a:  callvirt   instance int64 [mscorlib]System.IO.Stream::get_Position()
            IL_001f:  ldloc.1
            IL_0020:  callvirt   instance class [mscorlib]System.IO.Stream [mscorlib]System.IO.BinaryReader::get_BaseStream()
            IL_0025:  callvirt   instance int64 [mscorlib]System.IO.Stream::get_Length()
            IL_002a:  clt
            IL_002c:  ldc.i4.0
            IL_002d:  ceq
            IL_002f:  brfalse    IL_0039
            IL_0034:  leave      IL_0335
            IL_0039:  nop    IL_0014:  ldloc.1
            IL_0015:  callvirt   instance class [mscorlib]System.IO.Stream [mscorlib]System.IO.BinaryReader::get_BaseStream()
            IL_001a:  callvirt   instance int64 [mscorlib]System.IO.Stream::get_Position()
            IL_001f:  ldloc.1
            IL_0020:  callvirt   instance class [mscorlib]System.IO.Stream [mscorlib]System.IO.BinaryReader::get_BaseStream()
            IL_0025:  callvirt   instance int64 [mscorlib]System.IO.Stream::get_Length()
            IL_002a:  clt
            IL_002c:  ldc.i4.0
            IL_002d:  ceq
            IL_002f:  brfalse    IL_0039
            IL_0034:  leave      IL_0335
            IL_0039:  nop

            */
            var jump = methodCode.DefineLabel();
            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetProperty("BaseStream").GetGetMethod());
            methodCode.Emit(OpCodes.Callvirt, typeof(Stream).GetProperty("Position").GetGetMethod());
            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetProperty("BaseStream").GetGetMethod());
            methodCode.Emit(OpCodes.Callvirt, typeof(Stream).GetProperty("Length").GetGetMethod());
            methodCode.Emit(OpCodes.Clt);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Brfalse, jump);
            methodCode.Emit(OpCodes.Leave, exit);
            methodCode.MarkLabel(jump);
            methodCode.Emit(OpCodes.Nop);
        }

        private bool IsValueType(MemberInfo member)
        {
            var memberType = MemberType(member);
            return IsValueType(memberType);
        }

        private bool IsValueType(Type type)
        {
            return type == typeof(bool)
                || type == typeof(byte)
                || type == typeof(sbyte)
                || type == typeof(char)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(int)
                || type == typeof(uint)
                || type == typeof(long)
                || type == typeof(ulong)
                || type == typeof(float)
                || type == typeof(double)
                || type == typeof(decimal)
                || type.IsEnum
                ;
        }

        private bool IsNullableValueType(MemberInfo member)
        {
            return IsNullableValueType(MemberType(member));
        }
        private bool IsNullableValueType(Type type)
        {
            return type == typeof(bool?)
                || type == typeof(byte?)
                || type == typeof(sbyte?)
                || type == typeof(char?)
                || type == typeof(short?)
                || type == typeof(ushort?)
                || type == typeof(int?)
                || type == typeof(uint?)
                || type == typeof(long?)
                || type == typeof(ulong?)
                || type == typeof(float?)
                || type == typeof(double?)
                || type == typeof(decimal?)
                || (type.IsGenericType && type.Implements(typeof(Nullable<>)) && type.GetGenericArguments()[0].IsEnum)
                ;
        }

        private Type MemberType(MemberInfo member)
        {
            if (member is FieldInfo)
                return ((FieldInfo)member).FieldType;
            else
                return ((PropertyInfo)member).PropertyType;
        }

        private ConstructorInfo ImplementCtor(TypeBuilder typeBuilder, Type interfaceType, FieldInfo protoBuffField)
        {
            /*

            C# ----------------------------------------------------------------------------
            public SimplePOCO_BinarySerializer()
            {
            }

            IL ----------------------------------------------------------------------------
            .method public hidebysig specialname rtspecialname 
            instance void  .ctor() cil managed
            {
              // Code size       8 (0x8)
              .maxstack  8
              IL_0000:  ldarg.0
              IL_0001:  ldc.i4.0
              IL_0002:  newarr     [mscorlib]System.Byte
              IL_0007:  stfld      uint8[] 'Altus.Suffūz.Protocols'.BinarySerializer_RoutablePayload::_bytes
              IL_0008:  ldarg.0
              IL_0009:  call       instance void ['Altus.Suffūz']'Altus.Suffūz.Protocols'.RoutablePayload::.ctor()
              IL_000a:  nop
              IL_000b:  ret
            } // end of method BinarySerializer_RoutablePayload::.ctor



            */

            var baseType = interfaceType.GetGenericArguments()[0];
            var ctor = baseType.GetConstructors().FirstOrDefault(c => c.GetCustomAttribute<CustomConstructorAttribute>() != null);
            if (ctor == null)
            {
                ctor = baseType.GetConstructor(new Type[0]);
            }
            var ctorParams = ctor.GetParameters().Select(pi => pi.ParameterType).ToArray();

            var ctorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                CallingConventions.Standard,
                ctorParams);
            
            var ctorCode = ctorBuilder.GetILGenerator();

            ctorCode.Emit(OpCodes.Ldarg_0);
            ctorCode.Emit(OpCodes.Ldc_I4_0);
            ctorCode.Emit(OpCodes.Newarr, typeof(byte[]));
            ctorCode.Emit(OpCodes.Stfld, protoBuffField);
            ctorCode.Emit(OpCodes.Ldarg_0);
            for(int p = 0; p < ctorParams.Length; p++)
            {
                ctorCode.Emit(OpCodes.Ldarg, p + 1);
            }
            ctorCode.Emit(OpCodes.Call, ctor);
            ctorCode.Emit(OpCodes.Ret);

            return ctorBuilder;
        }

        private MethodInfo ImplementOnSerialize(TypeBuilder typeBuilder, Type interfaceType, PropertyInfo protoBuff)
        {
            /*

            C# -------------------------------------------------------------------------------
            protected byte[] OnSerialize(object source)
            {
                RoutablePayload typed = (RoutablePayload)source;
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter br = new BinaryWriter(ms);
                    
                    [ ... SERIALIZE MEMBERS ... ]

                    return ms.ToArray();
                }
            }

            IL --------------------------------------------------------------------------------
            .method family hidebysig instance uint8[] 
            OnSerialize(object source) cil managed
            {
              // Code size       114 (0x72)
              .maxstack  3
              .locals init ([0] class ['Altus.Suffūz']'Altus.Suffūz.Protocols'.RoutablePayload typed,
                       [1] class [mscorlib]System.IO.MemoryStream ms,
                       [2] class [mscorlib]System.IO.BinaryWriter 'br',
                       [3] uint8[] V_3)
              IL_0000:  nop
              IL_0001:  ldarg.1
              IL_0002:  castclass  ['Altus.Suffūz']'Altus.Suffūz.Protocols'.RoutablePayload
              IL_0007:  stloc.0
              IL_0008:  newobj     instance void [mscorlib]System.IO.MemoryStream::.ctor()
              IL_000d:  stloc.1
              .try
              {
                IL_000e:  nop
                IL_000f:  ldloc.1
                IL_0010:  newobj     instance void [mscorlib]System.IO.BinaryWriter::.ctor(class [mscorlib]System.IO.Stream)
                
                [ ... SERIALIZE MEMBERS ... ]

                IL_0063:  leave.s    IL_0070
              }  // end .try
              finally
              {
                IL_0065:  ldloc.1
                IL_0066:  brfalse.s  IL_006f
                IL_0068:  ldloc.1
                IL_0069:  callvirt   instance void [mscorlib]System.IDisposable::Dispose()
                IL_006e:  nop
                IL_006f:  endfinally
              }  // end handler
              IL_0070:  ldloc.3
              IL_0071:  ret
            } // end of method BinarySerializer_RoutablePayload::OnSerialize

            */

            var name = "OnSeserialize";
            var baseType = interfaceType.GetGenericArguments()[0];
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                typeof(byte[]),
                new Type[] { typeof(object) }
                );
            var methodCode = methodBuilder.GetILGenerator();
            var exit = methodCode.DefineLabel();
            var endfinally = methodCode.DefineLabel();

            methodCode.DeclareLocal(baseType);
            methodCode.DeclareLocal(typeof(MemoryStream));
            methodCode.DeclareLocal(typeof(BinaryWriter));
            methodCode.DeclareLocal(typeof(byte[]));

            methodCode.Emit(OpCodes.Ldarg_1);
            methodCode.Emit(OpCodes.Castclass, baseType);
            methodCode.Emit(OpCodes.Stloc_0);
            methodCode.Emit(OpCodes.Newobj, typeof(MemoryStream).GetConstructor(new Type[0]));
            methodCode.Emit(OpCodes.Stloc_1);

            methodCode.BeginExceptionBlock();
            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Newobj, typeof(BinaryWriter).GetConstructor(new Type[] { typeof(Stream) }));
            methodCode.Emit(OpCodes.Stloc_2);

            SerializeMembers(typeBuilder, interfaceType, methodCode, exit);
            //SerializeProtocolBufferBytes(typeBuilder, interfaceType, methodCode, protoBuff);

            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Callvirt, typeof(MemoryStream).GetMethod("ToArray"));
            methodCode.Emit(OpCodes.Stloc_3);
            methodCode.Emit(OpCodes.Leave, exit);

            methodCode.BeginFinallyBlock();
            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Brfalse, endfinally);
            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Callvirt, typeof(IDisposable).GetMethod("Dispose"));
            methodCode.MarkLabel(endfinally);
            methodCode.EndExceptionBlock();

            methodCode.MarkLabel(exit);
            methodCode.Emit(OpCodes.Ldloc_3);
            methodCode.Emit(OpCodes.Ret);

            return methodBuilder;
        }

        private void SerializeProtocolBufferBytes(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo protoBuff)
        {
            /*
            IL_0013:  ldloc.2
            IL_0014:  ldloc.0
            IL_0015:  isinst     ['Altus.Suffūz']'Altus.Suffūz.Serialization'.IProtocolBuffer
            IL_001a:  brfalse.s  IL_0028
            IL_001c:  ldloc.2
            IL_001d:  ldloc.0
            IL_001e:  callvirt   instance uint8[] 'Altus.Suffūz.Tests'.SimplePOCO::get___ProtoBuffer()
            IL_0023:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(uint8[])
            IL_0028:  ldloc.1


            */

            // appends extra bytes from forward version to payload, if they exist
            var jump = methodCode.DefineLabel();

            // check type being serialized is a protocolbuffer
            methodCode.Emit(OpCodes.Ldloc_0); // object to read
            methodCode.Emit(OpCodes.Isinst, typeof(IProtocolBuffer));
            methodCode.Emit(OpCodes.Brfalse_S, jump);

            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc_0); // object to read
            methodCode.Emit(OpCodes.Callvirt, protoBuff.GetGetMethod());
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(byte[]) }, null));

            methodCode.MarkLabel(jump);
            methodCode.Emit(OpCodes.Nop);
        }

        private MethodInfo ImplementOnDeserialize(TypeBuilder typeBuilder, Type interfaceType, ConstructorInfo ctor, PropertyInfo protoBuff)
        {
            /*

            C# -------------------------------------------------------------------------------
            protected object OnDeserialize(byte[] source, Type targetType)
            {
                using (MemoryStream ms = new MemoryStream(source))
                {
                    BinaryReader br = new BinaryReader(ms);
                    BinarySerializer_RoutablePayload typed = new BinarySerializer_RoutablePayload();
  
                    [... MEMBER DESERIALIZATION ROUTINES ...]

                    return typed;
                }
            }

            IL --------------------------------------------------------------------------------
            .method family hidebysig instance object 
            OnDeserialize(uint8[] source,
                          class [mscorlib]System.Type targetType) cil managed
            {
              // Code size       196 (0xc4)
              .maxstack  3
              .locals init (
                       [0] class [mscorlib]System.IO.MemoryStream ms,
                       [1] class [mscorlib]System.IO.BinaryReader 'br',
                       [2] class 'Altus.Suffūz.Protocols'.BinarySerializer_RoutablePayload typed,
                       )
              IL_0000:  nop
              IL_0001:  ldarg.1
              IL_0002:  newobj     instance void [mscorlib]System.IO.MemoryStream::.ctor(uint8[])
              IL_0007:  stloc.0
              .try
              {
                IL_0008:  nop
                IL_0009:  ldloc.0
                IL_000a:  newobj     instance void [mscorlib]System.IO.BinaryReader::.ctor(class [mscorlib]System.IO.Stream)
                IL_000f:  stloc.1
                IL_0010:  newobj     instance void 'Altus.Suffūz.Protocols'.BinarySerializer_RoutablePayload::.ctor()
                IL_0015:  stloc.2
                
                [ CALLS TO HANDLE EACH SERIALIZED MEMBER ]

              }  // end .try
              finally
              {
                IL_00b6:  ldloc.0
                IL_00b7:  brfalse.s  IL_00c0
                IL_00b9:  ldloc.0
                IL_00ba:  callvirt   instance void [mscorlib]System.IDisposable::Dispose()
                IL_00bf:  nop
                IL_00c0:  endfinally
              }  // end handler
              IL_00c1:  ldloc.0
              IL_00c3:  ret
            } // end of method BinarySerializer_RoutablePayload::OnDeserialize


            */
            var baseType = interfaceType.GetGenericArguments()[0];
            var customCtor = baseType.GetConstructors().FirstOrDefault(c => c.GetCustomAttribute<CustomConstructorAttribute>() != null);

            var name = "OnDeserialize";
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                typeof(object),
                new Type[] { typeof(byte[]), typeof(Type) }
                );
            var methodCode = methodBuilder.GetILGenerator();
            var exit = methodCode.DefineLabel();
            var endfinally = methodCode.DefineLabel();

            methodCode.DeclareLocal(typeof(MemoryStream));
            methodCode.DeclareLocal(typeof(BinaryReader));
            methodCode.DeclareLocal(typeBuilder);
            methodCode.DeclareLocal(typeof(bool));

            methodCode.Emit(OpCodes.Ldarg_1);
            methodCode.Emit(OpCodes.Newobj, typeof(MemoryStream).GetConstructor(new Type[] { typeof(byte[]) }));
            methodCode.Emit(OpCodes.Stloc_0);

            methodCode.BeginExceptionBlock();
            methodCode.Emit(OpCodes.Ldloc_0);
            methodCode.Emit(OpCodes.Newobj, typeof(BinaryReader).GetConstructor(new Type[] { typeof(Stream) }));
            methodCode.Emit(OpCodes.Stloc_1);

            if (customCtor != null)
            {
                // create using custom ctor
                var customAttrib = customCtor.GetCustomAttribute<CustomConstructorAttribute>();
                var customCtorParams = customCtor.GetParameters().Select(pi => pi.ParameterType).ToArray();
                var customCtorCtor = customAttrib.CustomConstructor.GetConstructor(new Type[0]);
                var customCtorCreate = customAttrib.CustomConstructor.GetMethod("GetCtorArgs");
                var customParams = methodCode.DeclareLocal(typeof(object[]));
                methodCode.Emit(OpCodes.Newobj, customCtorCtor);
                methodCode.Emit(OpCodes.Callvirt, customCtorCreate); // gets the ctor args in an object[]
                methodCode.Emit(OpCodes.Stloc, 4);
                // load each arg as a cast input parameter
                for(int p = 0; p < customCtorParams.Length; p++)
                {
                    methodCode.Emit(OpCodes.Ldloc, 4); // load array
                    methodCode.Emit(OpCodes.Ldc_I4, p); // load array index
                    methodCode.Emit(OpCodes.Ldelem); // read array
                    methodCode.Emit(OpCodes.Castclass, customCtorParams[p]);  // push the cast array type on the stack
                }
            }

            // create using the ctor with optional parameters
            methodCode.Emit(OpCodes.Newobj, ctor);
            methodCode.Emit(OpCodes.Stloc_2);

            DeserializeMembers(typeBuilder, interfaceType, methodCode, exit);
            //DeserializeProtocolBufferBytes(typeBuilder, interfaceType, methodCode, protoBuff);

            methodCode.Emit(OpCodes.Leave, exit);

            methodCode.BeginFinallyBlock();
            methodCode.Emit(OpCodes.Ldloc_0);
            methodCode.Emit(OpCodes.Brfalse, endfinally);
            methodCode.Emit(OpCodes.Ldloc_0);
            methodCode.Emit(OpCodes.Callvirt, typeof(IDisposable).GetMethod("Dispose"));
            methodCode.MarkLabel(endfinally);
            methodCode.EndExceptionBlock();

            methodCode.MarkLabel(exit);
            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ret);

            return methodBuilder;
        }

        private void DeserializeProtocolBufferBytes(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo protoBuff)
        {
            /*

            IL_0014:  ldloc.1
            IL_0015:  isinst     ['Altus.Suffūz']'Altus.Suffūz.Serialization'.IProtocolBuffer
            IL_001a:  brfalse.s  IL_0028
            IL_001c:  ldloc.1
            IL_001d:  ldloc.0
            IL_001e:  call       uint8[] ['Altus.Suffūz']'Altus.Suffūz.IO'.StreamHelper::GetBytes(class [mscorlib]System.IO.Stream)
            IL_0023:  callvirt   instance void 'Altus.Suffūz.Tests'.SimplePOCO::set___ProtoBuffer(uint8[])


            */

            var jump = methodCode.DefineLabel();

            // check type being serialized is a protocolbuffer
            methodCode.Emit(OpCodes.Ldloc_2); // object to write
            methodCode.Emit(OpCodes.Isinst, typeof(IProtocolBuffer));
            methodCode.Emit(OpCodes.Brfalse_S, jump);

            methodCode.Emit(OpCodes.Ldloc_2); // object to read
            methodCode.Emit(OpCodes.Ldloc_0); // memory stream
            methodCode.Emit(OpCodes.Call, typeof(StreamHelper).GetMethod("GetBytes", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(Stream) }, null));
            methodCode.Emit(OpCodes.Callvirt, protoBuff.GetSetMethod());

            methodCode.MarkLabel(jump);
            methodCode.Emit(OpCodes.Nop);
        }

        private void ImplementDeserializeGenericStream(TypeBuilder typeBuilder, Type interfaceType, MethodInfo deserializeGeneric)
        {
            /*

            C# --------------------------------------------------------------------------
            public Altus.Suffūz.Protocols.RoutablePayload Deserialize(System.IO.Stream inputSource)
            {
                return Deserialize(StreamHelper.GetBytes(inputSource));
            }

            IL --------------------------------------------------------------------------
            .method public hidebysig newslot virtual final 
            instance class ['Altus.Suffūz']'Altus.Suffūz.Protocols'.RoutablePayload 
            Deserialize(class [mscorlib]System.IO.Stream inputSource) cil managed
            {
              // Code size       18 (0x12)
              .maxstack  2
              .locals init ([0] class ['Altus.Suffūz']'Altus.Suffūz.Protocols'.RoutablePayload V_0)
              IL_0000:  nop
              IL_0001:  ldarg.0
              IL_0002:  ldarg.1
              IL_0003:  call       uint8[] ['Altus.Suffūz']'Altus.Suffūz.IO'.StreamHelper::GetBytes(class [mscorlib]System.IO.Stream)
              IL_0008:  call       instance class ['Altus.Suffūz']'Altus.Suffūz.Protocols'.RoutablePayload 'Altus.Suffūz.Protocols'.BinarySerializer_RoutablePayload::Deserialize(uint8[])
              IL_000d:  stloc.0
              IL_000e:  br.s       IL_0010
              IL_0010:  ldloc.0
              IL_0011:  ret
            } // end of method BinarySerializer_RoutablePayload::Deserialize

           */

            var name = "Deserialize";
            var baseType = interfaceType.GetGenericArguments()[0];
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                baseType,
                new Type[] { typeof(Stream) }
                );
            var methodCode = methodBuilder.GetILGenerator();
            var exit = methodCode.DefineLabel();
            methodCode.DeclareLocal(typeBuilder);
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Ldarg_1);
            methodCode.Emit(OpCodes.Ldtoken, baseType);
            methodCode.Emit(OpCodes.Call, typeof(StreamHelper).GetMethod("GetBytes", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(Stream) }, null));
            methodCode.Emit(OpCodes.Call, deserializeGeneric);
            methodCode.Emit(OpCodes.Stloc_0);
            methodCode.Emit(OpCodes.Br, exit);
            methodCode.MarkLabel(exit);
            methodCode.Emit(OpCodes.Ldloc_0);
            methodCode.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(methodBuilder,
                GetInterfaceMethod(
                    interfaceType,
                    name,
                    new Type[] { typeof(Stream) }
                    ));
        }

        private void ImplementSerializeGenericStream(TypeBuilder typeBuilder, Type interfaceType, MethodInfo serializeGeneric)
        {
            /*

            C# --------------------------------------------------------------------------
            public void Serialize(Altus.Suffūz.Protocols.RoutablePayload source, System.IO.Stream outputStream)
            {
                StreamHelper.Copy(Serialize(source), outputStream);
            }

            IL --------------------------------------------------------------------------
            .method public hidebysig newslot virtual final 
            instance void  Serialize(class ['Altus.Suffūz']'Altus.Suffūz.Protocols'.RoutablePayload source,
                                     class [mscorlib]System.IO.Stream outputStream) cil managed
            {
              // Code size       16 (0x10)
              .maxstack  8
              IL_0000:  nop
              IL_0001:  ldarg.0
              IL_0002:  ldarg.1
              IL_0003:  call       instance uint8[] 'Altus.Suffūz.Protocols'.BinarySerializer_RoutablePayload::Serialize(class ['Altus.Suffūz']'Altus.Suffūz.Protocols'.RoutablePayload)
              IL_0008:  ldarg.2
              IL_0009:  call       void ['Altus.Suffūz']'Altus.Suffūz.IO'.StreamHelper::Copy(uint8[],
                                                                                               class [mscorlib]System.IO.Stream)
              IL_000e:  nop
              IL_000f:  ret
            } // end of method BinarySerializer_RoutablePayload::Serialize

            */

            var name = "Serialize";
            var baseType = interfaceType.GetGenericArguments()[0];
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                typeof(void),
                new Type[] { baseType, typeof(Stream) }
                );
            var methodCode = methodBuilder.GetILGenerator();

            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Ldarg_1);
            methodCode.Emit(OpCodes.Call, serializeGeneric);
            methodCode.Emit(OpCodes.Ldarg_2);
            methodCode.Emit(OpCodes.Call, typeof(StreamHelper).GetMethod("Copy", 
                BindingFlags.Public | BindingFlags.Static, 
                null, 
                new Type[] { typeof(byte[]), typeof(Stream) }, null));
            methodCode.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(methodBuilder,
                GetInterfaceMethod(
                    interfaceType,
                    name,
                    new Type[] { baseType, typeof(Stream) }
                    ));
        }

        private MethodInfo ImplementDeserializeGeneric(TypeBuilder typeBuilder, Type interfaceType, MethodInfo onDeserialize)
        {
            /*

            C# --------------------------------------------------------------------------
            public Altus.Suffūz.Protocols.RoutablePayload Deserialize(byte[] source)
            {
                return (Altus.Suffūz.Protocols.RoutablePayload)this.OnDeserialize(source, typeof(Altus.Suffūz.Protocols.RoutablePayload));
            }

            IL --------------------------------------------------------------------------
            .method public hidebysig newslot virtual final 
            instance class ['Altus.Suffūz']'Altus.Suffūz.Protocols'.RoutablePayload 
            Deserialize(uint8[] source) cil managed
            {
              // Code size       28 (0x1c)
              .maxstack  3
              .locals init ([0] class ['Altus.Suffūz']'Altus.Suffūz.Protocols'.RoutablePayload V_0)
              IL_0000:  nop
              IL_0001:  ldarg.0
              IL_0002:  ldarg.1
              IL_0003:  ldtoken    ['Altus.Suffūz']'Altus.Suffūz.Protocols'.RoutablePayload
              IL_0008:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
              IL_000d:  call       instance object 'Altus.Suffūz.Protocols'.BinarySerializer_RoutablePayload::OnDeserialize(uint8[],
                                                                                                                             class [mscorlib]System.Type)
              IL_0012:  castclass  ['Altus.Suffūz']'Altus.Suffūz.Protocols'.RoutablePayload
              IL_0017:  stloc.0
              IL_0018:  br.s       IL_001a
              IL_001a:  ldloc.0
              IL_001b:  ret
            } // end of method BinarySerializer_RoutablePayload::Deserialize

           */

            var name = "Deserialize";
            var baseType = interfaceType.GetGenericArguments()[0];
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                baseType,
                new Type[] { typeof(byte[]) }
                );
            var methodCode = methodBuilder.GetILGenerator();
            var exit = methodCode.DefineLabel();

            methodCode.DeclareLocal(baseType);
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Ldarg_1);
            methodCode.Emit(OpCodes.Ldtoken, baseType);
            methodCode.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
            methodCode.Emit(OpCodes.Call, onDeserialize);
            methodCode.Emit(OpCodes.Castclass, baseType);
            methodCode.Emit(OpCodes.Stloc_0);
            methodCode.Emit(OpCodes.Br, exit);
            methodCode.MarkLabel(exit);
            methodCode.Emit(OpCodes.Ldloc_0);
            methodCode.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(methodBuilder,
                GetInterfaceMethod(
                    interfaceType,
                    name,
                    new Type[] { typeof(byte[]) }
                    ));

            return methodBuilder;
        }

        private MethodInfo ImplementSerializeGeneric(TypeBuilder typeBuilder, Type interfaceType, MethodInfo onSerialize)
        {
            /*

            C# --------------------------------------------------------------------------
            public byte[] Serialize(RoutablePayload source)
            {
                return OnSerialize(source);
            }

            IL --------------------------------------------------------------------------
            .method public hidebysig newslot virtual final 
            instance uint8[]  Serialize(class ['Altus.Suffūz']'Altus.Suffūz.Protocols'.RoutablePayload source) cil managed
            {
              // Code size       13 (0xd)
              .maxstack  2
              .locals init ([0] uint8[] V_0)
              IL_0000:  nop
              IL_0001:  ldarg.0
              IL_0002:  ldarg.1
              IL_0003:  call       instance uint8[] 'Altus.Suffūz.Protocols'.BinarySerializer_RoutablePayload::OnSerialize(object)
              IL_0008:  stloc.0
              IL_0009:  br.s       IL_000b
              IL_000b:  ldloc.0
              IL_000c:  ret
            } // end of method BinarySerializer_RoutablePayload::Serialize

            */

            var name = "Serialize";
            var baseType = interfaceType.GetGenericArguments()[0];
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                typeof(byte[]),
                new Type[] { baseType }
                );
            var methodCode = methodBuilder.GetILGenerator();
            var exit = methodCode.DefineLabel();

            methodCode.DeclareLocal(typeof(byte[]));
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Ldarg_1);
            methodCode.Emit(OpCodes.Call, onSerialize);
            methodCode.Emit(OpCodes.Stloc_0);
            methodCode.Emit(OpCodes.Br, exit);
            methodCode.MarkLabel(exit);
            methodCode.Emit(OpCodes.Ldloc_0);
            methodCode.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(methodBuilder,
                GetInterfaceMethod(
                    interfaceType,
                    name,
                    new Type[] { baseType }
                    ));

            return methodBuilder;
        }

        private MethodInfo ImplementDeserialize(TypeBuilder typeBuilder, Type interfaceType, MethodInfo onDeserialize)
        {
            /*

            C# --------------------------------------------------------------------------
            public object Deserialize(byte[] source, Type targetType)
            {
                return OnDeserialize(source, targetType);
            }

            IL --------------------------------------------------------------------------
            .method public hidebysig newslot virtual final 
            instance object  Deserialize(uint8[] source, class [mscorlib]System.Type targetType) cil managed
            {
              // Code size       14 (0xe)
              .maxstack  3
              .locals init ([0] object V_0)
              IL_0000:  nop
              IL_0001:  ldarg.0
              IL_0002:  ldarg.1
              IL_0003:  ldarg.2
              IL_0004:  call       instance object 'Altus.Suffūz.Protocols'.BinarySerializer_RoutablePayload::OnDeserialize(uint8[], class [mscorlib]System.Type)
              IL_0009:  stloc.0
              IL_000a:  br.s       IL_000c
              IL_000c:  ldloc.0
              IL_000d:  ret
            } // end of method BinarySerializer_RoutablePayload::Deserialize

           */

            var name = "Deserialize";
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                typeof(object),
                new Type[] { typeof(byte[]), typeof(Type) }
                );
            var methodCode = methodBuilder.GetILGenerator();
            var exit = methodCode.DefineLabel();

            methodCode.DeclareLocal(typeof(object));
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Ldarg_1);
            methodCode.Emit(OpCodes.Ldarg_2);
            methodCode.Emit(OpCodes.Call, onDeserialize);
            methodCode.Emit(OpCodes.Stloc_0);
            methodCode.Emit(OpCodes.Br, exit);
            methodCode.MarkLabel(exit);
            methodCode.Emit(OpCodes.Ldloc_0);
            methodCode.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(methodBuilder,
                GetInterfaceMethod(
                    interfaceType,
                    name,
                    new Type[] { typeof(byte[]), typeof(Type) }
                    ));

            return methodBuilder;
        }

        private void ImplementSerialize(TypeBuilder typeBuilder, Type interfaceType, MethodInfo onSerialize)
        {
            /*

            C# --------------------------------------------------------------------------
            public byte[] Serialize(object source)
            {
                return OnSerialize(source);
            }

            IL --------------------------------------------------------------------------
            .method public hidebysig newslot virtual final 
            instance uint8[]  Serialize(object source) cil managed
            {
              // Code size       13 (0xd)
              .maxstack  2
              .locals init ([0] uint8[] V_0)
              IL_0000:  nop
              IL_0001:  ldarg.0
              IL_0002:  ldarg.1
              IL_0003:  call       instance uint8[] 'Altus.Suffūz.Protocols'.BinarySerializer_RoutablePayload::OnSerialize(object)
              IL_0008:  stloc.0
              IL_0009:  br.s       IL_000b
              IL_000b:  ldloc.0
              IL_000c:  ret
            } // end of method BinarySerializer_RoutablePayload::Serialize

            */

            var name = "Serialize";
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                typeof(byte[]),
                new Type[] { typeof(object) }
                );
            var methodCode = methodBuilder.GetILGenerator();
            var exit = methodCode.DefineLabel();

            methodCode.DeclareLocal(typeof(byte[]));
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Ldarg_1);
            methodCode.Emit(OpCodes.Call, onSerialize);
            methodCode.Emit(OpCodes.Stloc_0);
            methodCode.Emit(OpCodes.Br, exit);
            methodCode.MarkLabel(exit);
            methodCode.Emit(OpCodes.Ldloc_0);
            methodCode.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(methodBuilder,
                GetInterfaceMethod(
                    interfaceType,
                    name,
                    new Type[] { typeof(object) }
                    ));
        }

        //private MethodInfo ImplementDeserializeType(TypeBuilder typeBuilder, Type interfaceType)
        //{
        //    /*

        //    C# ------------------------------------------------------------------------------
        //    protected object DeserializeType(BinaryReader br)
        //    {
        //        return _BinarySerializer.Deserialize(br);
        //    }

        //    IL ------------------------------------------------------------------------------
        //    .method family hidebysig instance object 
        //    DeserializeType(class [mscorlib]System.IO.BinaryReader 'br') cil managed
        //    {
        //      // Code size       12 (0xc)
        //      .maxstack  1
        //      .locals init ([0] object V_0)
        //      IL_0000:  nop
        //      IL_0001:  ldarg.1
        //      IL_0002:  call       object ['Altus.Suffūz']'Altus.Suffūz.Serialization.Binary'._BinarySerializer::Deserialize(class [mscorlib]System.IO.BinaryReader)
        //      IL_0007:  stloc.0
        //      IL_0008:  br.s       IL_000a
        //      IL_000a:  ldloc.0
        //      IL_000b:  ret
        //    } // end of method BinarySerializer_RoutablePayload::DeserializeType

        //    */

        //    var name = "DeserializeType";
        //    var methodBuilder = typeBuilder.DefineMethod(name,
        //        MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
        //        typeof(object),
        //        new Type[] { typeof(object), typeof(BinaryWriter) }
        //        );
        //    var methodCode = methodBuilder.GetILGenerator();
        //    var exit = methodCode.DefineLabel();

        //    methodCode.DeclareLocal(typeof(object));
        //    methodCode.Emit(OpCodes.Ldarg_1);
        //    methodCode.Emit(OpCodes.Call, typeof(_BinarySerializer).GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static));
        //    methodCode.Emit(OpCodes.Stloc_0);
        //    methodCode.Emit(OpCodes.Br, exit);
        //    methodCode.MarkLabel(exit);
        //    methodCode.Emit(OpCodes.Ldloc_0);
        //    methodCode.Emit(OpCodes.Ret);

        //    return methodBuilder;
        //}

        //private MethodInfo ImplementSerializeType(TypeBuilder typeBuilder, Type interfaceType)
        //{
        //    /*
        //    C#
        //    protected void SerializeType(object source, BinaryWriter br)
        //    {
        //        BinarySerializerBuilder._BinarySerializer.Serialize(source, br);
        //    }

        //    IL --------------------------------------------------------------------------------
        //    .method family hidebysig instance void  SerializeType(object source,
        //                                              class [mscorlib]System.IO.BinaryWriter 'br') cil managed
        //    {
        //      // Code size       10 (0xa)
        //      .maxstack  8
        //      IL_0000:  nop
        //      IL_0001:  ldarg.1
        //      IL_0002:  ldarg.2
        //      IL_0003:  call       void ['Altus.Suffūz']'Altus.Suffūz.Serialization.Binary'.BinarySerializerBuilder/_BinarySerializer::Serialize(object,
        //                                                                                                                                           class [mscorlib]System.IO.BinaryWriter)
        //      IL_0008:  nop
        //      IL_0009:  ret
        //    } // end of method BinarySerializer_RoutablePayload::SerializeType

        //    */
        //    var name = "SerializeType";
        //    var methodBuilder = typeBuilder.DefineMethod(name,
        //        MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
        //        typeof(void),
        //        new Type[] { typeof(object), typeof(BinaryWriter) }
        //        );
        //    var methodCode = methodBuilder.GetILGenerator();
        //    var exit = methodCode.DefineLabel();

        //    methodCode.Emit(OpCodes.Ldarg_1);
        //    methodCode.Emit(OpCodes.Ldarg_2);
        //    methodCode.Emit(OpCodes.Call, typeof(_BinarySerializer).GetMethod("Serialize", BindingFlags.Public | BindingFlags.Static));
        //    methodCode.Emit(OpCodes.Ret);

        //    return methodBuilder;
        //}

        private void ImplementSupportsType(TypeBuilder typeBuilder, Type interfaceType)
        {
            /*
            C# ----------------------------------------------------------------------------------------------
            public bool SupportsType(Type type)
            {
                return type == this.GetType().BaseType 
                    || typeof(ISerializer<Altus.Suffūz.Protocols.RoutablePayload>).IsAssignableFrom(type);
            }

            IL ----------------------------------------------------------------------------------------------
            .method public hidebysig newslot virtual final 
            instance bool  SupportsType(class [mscorlib]System.Type 'type') cil managed
            {
              // Code size       44 (0x2c)
              .maxstack  2
              .locals init ([0] bool V_0)
              IL_0000:  nop
              IL_0001:  ldarg.1
              IL_0002:  ldarg.0
              IL_0003:  call       instance class [mscorlib]System.Type [mscorlib]System.Object::GetType()
              IL_0008:  callvirt   instance class [mscorlib]System.Type [mscorlib]System.Type::get_BaseType()
              IL_000d:  call       bool [mscorlib]System.Type::op_Equality(class [mscorlib]System.Type,
                                                                           class [mscorlib]System.Type)
              IL_0012:  brtrue.s   IL_0026
              IL_0014:  ldtoken    class ['Altus.Suffūz']'Altus.Suffūz.Serialization'.ISerializer`1<class ['Altus.Suffūz']'Altus.Suffūz.Protocols'.RoutablePayload>
              IL_0019:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
              IL_001e:  ldarg.1
              IL_001f:  callvirt   instance bool [mscorlib]System.Type::IsAssignableFrom(class [mscorlib]System.Type)
              IL_0024:  br.s       IL_0027
              IL_0026:  ldc.i4.1
              IL_0027:  stloc.0
              IL_0028:  br.s       IL_002a
              IL_002a:  ldloc.0
              IL_002b:  ret
            } // end of method BinarySerializer_RoutablePayload::SupportsType

            */

            var name = "SupportsType";
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                typeof(bool),
                new Type[] { typeof(Type) }
                );
            var methodCode = methodBuilder.GetILGenerator();
            var exit = methodCode.DefineLabel();
            var l1 = methodCode.DefineLabel();
            var l2 = methodCode.DefineLabel();

            methodCode.DeclareLocal(typeof(bool)); // return value
            methodCode.Emit(OpCodes.Ldarg_1);
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, typeof(Object).GetMethod("GetType"));
            methodCode.Emit(OpCodes.Callvirt, typeof(Type).GetProperty("BaseType").GetGetMethod());
            methodCode.Emit(OpCodes.Call, typeof(Type).GetMethod("Equals", new Type[] { typeof(Type) }));
            methodCode.Emit(OpCodes.Brtrue, l1);
            methodCode.Emit(OpCodes.Ldtoken, interfaceType);
            methodCode.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
            methodCode.Emit(OpCodes.Ldarg_1);
            methodCode.Emit(OpCodes.Callvirt, typeof(Type).GetMethod("IsAssignableFrom"));
            methodCode.Emit(OpCodes.Br, l2);
            methodCode.MarkLabel(l1);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.MarkLabel(l2);
            methodCode.Emit(OpCodes.Stloc_0);
            methodCode.Emit(OpCodes.Br, exit);
            methodCode.MarkLabel(exit);
            methodCode.Emit(OpCodes.Ldloc_0);
            methodCode.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(methodBuilder,
                GetInterfaceMethod(
                    interfaceType,
                    name,
                    new Type[] { typeof(Type) }
                    ));
        }

        private void ImplementSupportsFormat(TypeBuilder typeBuilder, Type interfaceType)
        {
            /*
            C# ----------------------------------------------------------------------------------------------
            public bool SupportsFormat(string format)
            {
                return format.Equals(StandardFormats.BINARY, StringComparison.InvariantCultureIgnoreCase);
            }

            IL ----------------------------------------------------------------------------
            .method public hidebysig newslot virtual final 
            instance bool  SupportsFormat(string format) cil managed
            {
              // Code size       18 (0x12)
              .maxstack  3
              .locals init ([0] bool V_0)
              IL_0000:  nop
              IL_0001:  ldarg.1
              IL_0002:  ldstr      "bin"
              IL_0007:  ldc.i4.3
              IL_0008:  callvirt   instance bool [mscorlib]System.String::Equals(string, valuetype [mscorlib]System.StringComparison)
              IL_000d:  stloc.0
              IL_000e:  br.s       IL_0010
              IL_0010:  ldloc.0
              IL_0011:  ret
            } // end of method BinarySerializer_RoutablePayload::SupportsFormat

            */
            var name = "SupportsFormat";
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                typeof(bool),
                new Type[] { typeof(string) }
                );
            var methodCode = methodBuilder.GetILGenerator();
            var exit = methodCode.DefineLabel();

            methodCode.DeclareLocal(typeof(bool));
            methodCode.Emit(OpCodes.Ldarg_1);
            methodCode.Emit(OpCodes.Ldstr, StandardFormats.BINARY);
            methodCode.Emit(OpCodes.Ldc_I4_3);
            methodCode.Emit(OpCodes.Callvirt, typeof(string).GetMethod("Equals", new Type[] { typeof(string), typeof(StringComparison) }));
            methodCode.Emit(OpCodes.Stloc_0);
            methodCode.Emit(OpCodes.Br, exit);
            methodCode.MarkLabel(exit);
            methodCode.Emit(OpCodes.Ldloc_0);
            methodCode.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(methodBuilder, 
                GetInterfaceMethod(
                    interfaceType,
                    name, 
                    new Type[] { typeof(string) }
                    )
                );
        }

        private void ImplementPriority(TypeBuilder typeBuilder, Type interfaceType)
        {
            var name = "Priority";
            var property = typeBuilder.DefineProperty(name, PropertyAttributes.HasDefault, typeof(int), null);
            var field = typeBuilder.DefineField("_" + name, typeof(int), FieldAttributes.Private);
            var getter = typeBuilder.DefineMethod("get_" + name,
                      MethodAttributes.Public
                    | MethodAttributes.SpecialName
                    | MethodAttributes.HideBySig
                    | MethodAttributes.NewSlot
                    | MethodAttributes.Final
                    | MethodAttributes.Virtual,
                      typeof(int), 
                      Type.EmptyTypes);
            var setter = typeBuilder.DefineMethod("set_" + name,
                      MethodAttributes.Public
                    | MethodAttributes.SpecialName
                    | MethodAttributes.HideBySig
                    | MethodAttributes.NewSlot
                    | MethodAttributes.Final
                    | MethodAttributes.Virtual,
                    null,
                    new[] { typeof(int) });

            var getterCode = getter.GetILGenerator();
            getterCode.Emit(OpCodes.Ldarg_0);
            getterCode.Emit(OpCodes.Ldfld, field);
            getterCode.Emit(OpCodes.Ret);
            typeBuilder.DefineMethodOverride(getter, GetInterfaceMember<PropertyInfo>(interfaceType, name).GetGetMethod());
            property.SetGetMethod(getter);

            var setterCode = setter.GetILGenerator();
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Ldarg_1);
            setterCode.Emit(OpCodes.Stfld, field);
            setterCode.Emit(OpCodes.Ret);
            property.SetSetMethod(setter); // interface does not declare a setter
        }

        private void ImplementIsScalar(TypeBuilder typeBuilder, Type interfaceType)
        {
            var name = "IsScalar";
            var property = typeBuilder.DefineProperty(name, PropertyAttributes.HasDefault, typeof(bool), null);
            var getter = typeBuilder.DefineMethod("get_" + name,
                      MethodAttributes.Public
                    | MethodAttributes.SpecialName
                    | MethodAttributes.HideBySig
                    | MethodAttributes.NewSlot
                    | MethodAttributes.Final
                    | MethodAttributes.Virtual,
                      typeof(bool), 
                      Type.EmptyTypes);

            var getterCode = getter.GetILGenerator();
            var local = getterCode.DeclareLocal(typeof(bool));
            var label = getterCode.DefineLabel();
            getterCode.Emit(OpCodes.Ldloc_0);
            getterCode.Emit(OpCodes.Stloc_0);
            getterCode.Emit(OpCodes.Br, label);
            getterCode.MarkLabel(label);
            getterCode.Emit(OpCodes.Ldloc_0);
            getterCode.Emit(OpCodes.Ret);
            typeBuilder.DefineMethodOverride(getter, GetInterfaceMember<PropertyInfo>(interfaceType, name).GetGetMethod());
            property.SetGetMethod(getter);
        }

        public ISerializer<T> CreateSerializerType<T>()
        {
            return (ISerializer<T>)CreateSerializerType(typeof(T));
        }

        public Func<ISerializer> CreateActivator(Type type)
        {
            var ctorExpression = Expression.New(type.GetConstructor(new Type[0]));
            return Expression.Lambda<Func<ISerializer>>(ctorExpression).Compile();
        }

        public static MethodInfo GetInterfaceMethod(Type interfaceType, string methodName, Type[] parameterTypes)
        {
            return GetInterfaceMembers(interfaceType).OfType<MethodInfo>().FirstOrDefault(m => m.Name == methodName 
            && ParametersMatch(m.GetParameters().Select(p => p.ParameterType).ToArray(), parameterTypes));
        }

        private static bool ParametersMatch(Type[] params1, Type[] params2)
        {
            var areEqual = params1.Length == params2.Length;
            if (areEqual)
            {
                for (int i = 0; i < params1.Length; i++)
                {
                    if (!params1[i].Equals(params2[i]))
                    {
                        areEqual = false;
                        break;
                    }
                }
            }
            return areEqual;
        }

        public static T GetInterfaceMember<T>(Type interfaceType, string memberName) where T : MemberInfo
        {
            return GetInterfaceMembers(interfaceType).OfType<T>().FirstOrDefault(m => m.Name == memberName);
        }

        /// <summary>
        /// Specifically built to provide a complete list of declared and inherited members for interface type declarations
        /// </summary>
        /// <param name="interfaceType"></param>
        /// <returns></returns>
        public static IEnumerable<MemberInfo> GetInterfaceMembers(Type interfaceType)
        {
            if (!interfaceType.IsInterface) throw new InvalidOperationException("This method is design to work for interfaces only.");

            List<MemberInfo> members = new List<MemberInfo>();
            GetInterfaceMembersRecurse(interfaceType, ref members);
            return members;
        }

        private static void GetInterfaceMembersRecurse(Type interfaceType, ref List<MemberInfo> members)
        {
            foreach (var t in interfaceType.FindInterfaces((a, b) => true, true))
            {
                //if ((interfaceType.BaseType != null && !interfaceType.BaseType.Equals(typeof(object))))
                GetInterfaceMembersRecurse(t, ref members);
            }

            foreach (var member in interfaceType.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!members.Contains(member, new MemberInfoComparer()))
                    members.Add(member);
            }
        }
    }

    public class MemberInfoComparer : IEqualityComparer<MemberInfo>
    {
        public bool Equals(MemberInfo x, MemberInfo y)
        {
            var equals = x != null
                && y != null
                && x.Name.Equals(y.Name)
                && x.MemberType.Equals(y.MemberType)
                && (x is MethodInfo && y is MethodInfo ? ParametersEqual((MethodInfo)x, (MethodInfo)y) : false);
            return equals;
        }

        private bool ParametersEqual(MethodInfo methodInfo1, MethodInfo methodInfo2)
        {
            ParameterInfo[] p1 = methodInfo1.GetParameters();
            Type pRet1 = methodInfo1.ReturnType;
            ParameterInfo[] p2 = methodInfo2.GetParameters();
            Type pRet2 = methodInfo1.ReturnType;
            return pRet1.Equals(pRet2)
                && ParametersEqual(p1, p2)
                && GenericArgsEqual(methodInfo1, methodInfo2);

        }

        private bool GenericArgsEqual(MethodInfo methodInfo1, MethodInfo methodInfo2)
        {
            return methodInfo1.IsGenericMethod == methodInfo2.IsGenericMethod
                && methodInfo1.IsGenericMethod ? GenericArgsEqual(methodInfo1.GetGenericArguments(), methodInfo2.GetGenericArguments()) : true;
        }

        private bool GenericArgsEqual(Type[] type1, Type[] type2)
        {
            var areEqual = type1.Length == type2.Length;
            if (areEqual)
            {
                for (int i = 0; i < type1.Length; i++)
                {
                    if (!type1[i].Equals(type2[i]))
                    {
                        areEqual = false;
                        break;
                    }
                }
            }
            return areEqual;
        }

        private bool ParametersEqual(ParameterInfo[] p1, ParameterInfo[] p2)
        {
            if (p1.Length.Equals(p2.Length))
            {
                for (int p = 0; p < p1.Length; p++)
                {
                    if (!p1[p].ParameterType.Equals(p2[p].ParameterType)) return false;
                }
                return true;
            }
            else return false;
        }

        public int GetHashCode(MemberInfo obj)
        {
            return obj.GetHashCode();
        }
    }
}
