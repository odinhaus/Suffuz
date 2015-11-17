using Altus.Suffūz.IO;
using System;
using System.Collections.Generic;
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
            lock(_typeCache)
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
            var className = type.Namespace + "." + type.Name + "_BinarySerializer";
            var typeBuilder = _modBuilder.DefineType(
                className, 
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable, 
                type, // base type
                new Type[] { interfaceType } // interfaces
                );
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

            var ctor = ImplementCtor(typeBuilder, interfaceType);

            ImplementIsScalar(typeBuilder, interfaceType);
            ImplementPriority(typeBuilder, interfaceType);
            ImplementSupportsFormat(typeBuilder, interfaceType);
            ImplementSupportsType(typeBuilder, interfaceType);

            var serializeType = ImplementSerializeType(typeBuilder, interfaceType);
            var deserializeType = ImplementDeserializeType(typeBuilder, interfaceType);

            var onSerialize = ImplementOnSerialize(typeBuilder, interfaceType, serializeType);
            var onDeserialize = ImplementOnDeserialize(typeBuilder, interfaceType, ctor, deserializeType);

            
            ImplementSerialize(typeBuilder, interfaceType, onSerialize);
            ImplementDeserialize(typeBuilder, interfaceType, onDeserialize);
            var serializeGeneric = ImplementSerializeGeneric(typeBuilder, interfaceType, onSerialize);
            var deserializeGeneric = ImplementDeserializeGeneric(typeBuilder, interfaceType, onDeserialize);
            ImplementSerializeGenericStream(typeBuilder, interfaceType, serializeGeneric);
            ImplementDeserializeGenericStream(typeBuilder, interfaceType, deserializeGeneric);
            

            return typeBuilder.CreateType();
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
                }
            }
        }

        private void SerializeByteArray(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            /*

            C# ----------------------------------------------------------------------------------------------------
            byte[] n = epoco.N;
            int length = n.Length;
            writer.Write(length);
            writer.Write(n);

            IL ----------------------------------------------------------------------------------------------------
            IL_00b0:  ldloc.2
            IL_00b1:  ldloc.0
            IL_00b2:  callvirt   instance uint8[] ['Altus.Suffūz.Tests']'Altus.Suffūz.Tests'.SimplePOCO::get_N()
            IL_00b7:  stloc.s    V_4
            IL_00b9:  ldloc.s    V_4
            IL_00bb:  callvirt   instance int32 [mscorlib]System.Array::get_Length()
            IL_00c0:  stloc.s    V_5
            IL_00c2:  ldloc.2
            IL_00c3:  ldloc.s    V_5
            IL_00c5:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(int32)
            IL_00ca:  ldloc.2
            IL_00cb:  ldloc.s    V_4
            IL_00cd:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(uint8[])


            */
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc_0); // object to read
            var array = methodCode.DeclareLocal(typeof(byte[]));
            var arrayLength = methodCode.DeclareLocal(typeof(int));
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Callvirt, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetGetMethod());
            }
            methodCode.Emit(OpCodes.Stloc, array);
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Callvirt, typeof(byte[]).GetProperty("Length").GetGetMethod());
            methodCode.Emit(OpCodes.Stloc, arrayLength);
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc, arrayLength);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(int) }));
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { MemberType(member) }));
        }

        private void SerializeCharArray(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            /*

            C# ----------------------------------------------------------------------------------------------------
            byte[] n = epoco.N;
            int length = n.Length;
            writer.Write(length);
            writer.Write(n);

            IL ----------------------------------------------------------------------------------------------------
            IL_00b0:  ldloc.2
            IL_00b1:  ldloc.0
            IL_00b2:  callvirt   instance uint8[] ['Altus.Suffūz.Tests']'Altus.Suffūz.Tests'.SimplePOCO::get_N()
            IL_00b7:  stloc.s    V_4
            IL_00b9:  ldloc.s    V_4
            IL_00bb:  callvirt   instance int32 [mscorlib]System.Array::get_Length()
            IL_00c0:  stloc.s    V_5
            IL_00c2:  ldloc.2
            IL_00c3:  ldloc.s    V_5
            IL_00c5:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(int32)
            IL_00ca:  ldloc.2
            IL_00cb:  ldloc.s    V_4
            IL_00cd:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(uint8[])


            */
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc_0); // object to read
            var array = methodCode.DeclareLocal(typeof(char[]));
            var arrayLength = methodCode.DeclareLocal(typeof(int));
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Callvirt, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetGetMethod());
            }
            methodCode.Emit(OpCodes.Stloc, array);
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Callvirt, typeof(char[]).GetProperty("Length").GetGetMethod());
            methodCode.Emit(OpCodes.Stloc, arrayLength);
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc, arrayLength);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(int) }));
            methodCode.Emit(OpCodes.Ldloc_2); // binary writer
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { MemberType(member) }));
        }

        private void SerializeValueType(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
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
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { MemberType(member) }));
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
                }
            }
        }

        private void DeserializeByteArray(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            /*
            methodCode.DeclareLocal(typeof(MemoryStream));
            methodCode.DeclareLocal(typeof(BinaryReader));
            methodCode.DeclareLocal(typeBuilder);
            methodCode.DeclareLocal(typeof(bool));
            */
            var array = methodCode.DeclareLocal(typeof(byte[]));
            var arrayLength = methodCode.DeclareLocal(typeof(int));

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
        }

        private void DeserializeCharArray(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            /*
            methodCode.DeclareLocal(typeof(MemoryStream));
            methodCode.DeclareLocal(typeof(BinaryReader));
            methodCode.DeclareLocal(typeBuilder);
            methodCode.DeclareLocal(typeof(bool));
            */
            var array = methodCode.DeclareLocal(typeof(char[]));
            var arrayLength = methodCode.DeclareLocal(typeof(int));

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
        }

        private void DeserializeValueType(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, MemberInfo member)
        {
            var type = MemberType(member);
            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Ldloc_1);
            if (type == typeof(bool))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            }
            else if (type == typeof(byte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadByte"));
            }
            else if (type == typeof(sbyte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSByte"));
            }
            else if (type == typeof(char))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadChar"));
            }
            else if (type == typeof(short))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt16"));
            }
            else if (type == typeof(ushort))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt16"));
            }
            else if (type == typeof(int))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            }
            else if (type == typeof(uint))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt32"));
            }
            else if (type == typeof(long))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            }
            else if (type == typeof(ulong))
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
            if (member is FieldInfo)
            {
                methodCode.Emit(OpCodes.Stfld, (FieldInfo)member);
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetSetMethod());
            }
        }

        private void CheckStreamPosition(ILGenerator methodCode, Label exit)
        {
            /*

            IL_0016:  ldloc.1
            IL_0017:  callvirt   instance class [mscorlib]System.IO.Stream [mscorlib]System.IO.BinaryReader::get_BaseStream()
            IL_001c:  callvirt   instance int64 [mscorlib]System.IO.Stream::get_Position()
            IL_0021:  ldloc.1
            IL_0022:  callvirt   instance class [mscorlib]System.IO.Stream [mscorlib]System.IO.BinaryReader::get_BaseStream()
            IL_0027:  callvirt   instance int64 [mscorlib]System.IO.Stream::get_Length()
            IL_002c:  clt
            IL_002e:  ldc.i4.0
            IL_002f:  ceq

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
            return memberType == typeof(bool)
                || memberType == typeof(byte)
                || memberType == typeof(sbyte)
                || memberType == typeof(char)
                || memberType == typeof(short)
                || memberType == typeof(ushort)
                || memberType == typeof(int)
                || memberType == typeof(uint)
                || memberType == typeof(long)
                || memberType == typeof(ulong)
                || memberType == typeof(float)
                || memberType == typeof(double)
                || memberType == typeof(decimal)
                ;
        }

        private Type MemberType(MemberInfo member)
        {
            if (member is FieldInfo)
                return ((FieldInfo)member).FieldType;
            else
                return ((PropertyInfo)member).PropertyType;
        }

        private ConstructorInfo ImplementCtor(TypeBuilder typeBuilder, Type interfaceType)
        {
            /*

            .method public hidebysig specialname rtspecialname 
            instance void  .ctor() cil managed
            {
              // Code size       8 (0x8)
              .maxstack  8
              IL_0000:  ldarg.0
              IL_0001:  call       instance void ['Altus.Suffūz']'Altus.Suffūz.Protocols'.RoutablePayload::.ctor()
              IL_0006:  nop
              IL_0007:  ret
            } // end of method BinarySerializer_RoutablePayload::.ctor

            */

            var ctorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                CallingConventions.Standard,
                new Type[0]);
            var baseType = interfaceType.GetGenericArguments()[0];
            var ctorCode = ctorBuilder.GetILGenerator();
            ctorCode.Emit(OpCodes.Ldarg_0);
            ctorCode.Emit(OpCodes.Call, baseType.GetConstructor(new Type[0]));
            ctorCode.Emit(OpCodes.Ret);
            return ctorBuilder;
        }

        private MethodInfo ImplementOnSerialize(TypeBuilder typeBuilder, Type interfaceType, MethodInfo serializeType)
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

        private MethodInfo ImplementOnDeserialize(TypeBuilder typeBuilder, Type interfaceType, ConstructorInfo ctor, MethodInfo deserializeType)
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

            var name = "OnDeserialize";
            var baseType = interfaceType.GetGenericArguments()[0];
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

            methodCode.Emit(OpCodes.Newobj, ctor);
            methodCode.Emit(OpCodes.Stloc_2);

            DeserializeMembers(typeBuilder, interfaceType, methodCode, exit);

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

        private MethodInfo ImplementDeserializeType(TypeBuilder typeBuilder, Type interfaceType)
        {
            /*

            C# ------------------------------------------------------------------------------
            protected object DeserializeType(BinaryReader br)
            {
                return _BinarySerializer.Deserialize(br);
            }

            IL ------------------------------------------------------------------------------
            .method family hidebysig instance object 
            DeserializeType(class [mscorlib]System.IO.BinaryReader 'br') cil managed
            {
              // Code size       12 (0xc)
              .maxstack  1
              .locals init ([0] object V_0)
              IL_0000:  nop
              IL_0001:  ldarg.1
              IL_0002:  call       object ['Altus.Suffūz']'Altus.Suffūz.Serialization.Binary'._BinarySerializer::Deserialize(class [mscorlib]System.IO.BinaryReader)
              IL_0007:  stloc.0
              IL_0008:  br.s       IL_000a
              IL_000a:  ldloc.0
              IL_000b:  ret
            } // end of method BinarySerializer_RoutablePayload::DeserializeType

            */

            var name = "DeserializeType";
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                typeof(object),
                new Type[] { typeof(object), typeof(BinaryWriter) }
                );
            var methodCode = methodBuilder.GetILGenerator();
            var exit = methodCode.DefineLabel();

            methodCode.DeclareLocal(typeof(object));
            methodCode.Emit(OpCodes.Ldarg_1);
            methodCode.Emit(OpCodes.Call, typeof(_BinarySerializer).GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Stloc_0);
            methodCode.Emit(OpCodes.Br, exit);
            methodCode.MarkLabel(exit);
            methodCode.Emit(OpCodes.Ldloc_0);
            methodCode.Emit(OpCodes.Ret);

            return methodBuilder;
        }

        private MethodInfo ImplementSerializeType(TypeBuilder typeBuilder, Type interfaceType)
        {
            /*
            C#
            protected void SerializeType(object source, BinaryWriter br)
            {
                BinarySerializerBuilder._BinarySerializer.Serialize(source, br);
            }

            IL --------------------------------------------------------------------------------
            .method family hidebysig instance void  SerializeType(object source,
                                                      class [mscorlib]System.IO.BinaryWriter 'br') cil managed
            {
              // Code size       10 (0xa)
              .maxstack  8
              IL_0000:  nop
              IL_0001:  ldarg.1
              IL_0002:  ldarg.2
              IL_0003:  call       void ['Altus.Suffūz']'Altus.Suffūz.Serialization.Binary'.BinarySerializerBuilder/_BinarySerializer::Serialize(object,
                                                                                                                                                   class [mscorlib]System.IO.BinaryWriter)
              IL_0008:  nop
              IL_0009:  ret
            } // end of method BinarySerializer_RoutablePayload::SerializeType

            */
            var name = "SerializeType";
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                typeof(void),
                new Type[] { typeof(object), typeof(BinaryWriter) }
                );
            var methodCode = methodBuilder.GetILGenerator();
            var exit = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldarg_1);
            methodCode.Emit(OpCodes.Ldarg_2);
            methodCode.Emit(OpCodes.Call, typeof(_BinarySerializer).GetMethod("Serialize", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Ret);

            return methodBuilder;
        }

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
