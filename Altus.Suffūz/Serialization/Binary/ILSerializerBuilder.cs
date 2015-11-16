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
    public class ILSerializerBuilder : IBinarySerializerBuilder
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

            ImplementIsScalar(typeBuilder, interfaceType);
            ImplementPriority(typeBuilder, interfaceType);
            ImplementSupportsFormat(typeBuilder, interfaceType);
            ImplementSupportsType(typeBuilder, interfaceType);
            ImplementSerialize(typeBuilder, interfaceType);
            ImplementDeserialize(typeBuilder, interfaceType);
            ImplementSerializeGeneric(typeBuilder, interfaceType);
            ImplementDeserializeGeneric(typeBuilder, interfaceType);
            ImplementSerializeGenericStream(typeBuilder, interfaceType);
            ImplementDeserializeGenericStream(typeBuilder, interfaceType);

            return typeBuilder.CreateType();
        }

        private void ImplementDeserializeGenericStream(TypeBuilder typeBuilder, Type interfaceType)
        {
            var name = "Deserialize";
            var baseType = interfaceType.GetGenericArguments()[0];
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                baseType,
                new Type[] { typeof(Stream) }
                );
            var methodCode = methodBuilder.GetILGenerator();
            methodCode.ThrowException(typeof(NotImplementedException));
            typeBuilder.DefineMethodOverride(methodBuilder,
                GetInterfaceMethod(
                    interfaceType,
                    name,
                    new Type[] { typeof(Stream) }
                    ));
        }

        private void ImplementSerializeGenericStream(TypeBuilder typeBuilder, Type interfaceType)
        {
            var name = "Serialize";
            var baseType = interfaceType.GetGenericArguments()[0];
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                typeof(void),
                new Type[] { baseType, typeof(Stream) }
                );
            var methodCode = methodBuilder.GetILGenerator();
            methodCode.ThrowException(typeof(NotImplementedException));
            typeBuilder.DefineMethodOverride(methodBuilder,
                GetInterfaceMethod(
                    interfaceType,
                    name,
                    new Type[] { baseType, typeof(Stream) }
                    ));
        }

        private void ImplementDeserializeGeneric(TypeBuilder typeBuilder, Type interfaceType)
        {
            var name = "Deserialize";
            var baseType = interfaceType.GetGenericArguments()[0];
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                baseType,
                new Type[] { typeof(byte[]) }
                );
            var methodCode = methodBuilder.GetILGenerator();
            methodCode.ThrowException(typeof(NotImplementedException));
            typeBuilder.DefineMethodOverride(methodBuilder,
                GetInterfaceMethod(
                    interfaceType,
                    name,
                    new Type[] { typeof(byte[]) }
                    ));
        }

        private void ImplementSerializeGeneric(TypeBuilder typeBuilder, Type interfaceType)
        {
            var name = "Serialize";
            var baseType = interfaceType.GetGenericArguments()[0];
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                typeof(byte[]),
                new Type[] { baseType }
                );
            var methodCode = methodBuilder.GetILGenerator();
            methodCode.ThrowException(typeof(NotImplementedException));
            typeBuilder.DefineMethodOverride(methodBuilder,
                GetInterfaceMethod(
                    interfaceType,
                    name,
                    new Type[] { baseType }
                    ));
        }

        private void ImplementDeserialize(TypeBuilder typeBuilder, Type interfaceType)
        {
            var name = "Deserialize";
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                typeof(object),
                new Type[] { typeof(byte[]), typeof(Type) }
                );
            var methodCode = methodBuilder.GetILGenerator();
            methodCode.ThrowException(typeof(NotImplementedException));
            typeBuilder.DefineMethodOverride(methodBuilder,
                GetInterfaceMethod(
                    interfaceType,
                    name,
                    new Type[] { typeof(byte[]), typeof(Type) }
                    ));
        }

        private void ImplementSerialize(TypeBuilder typeBuilder, Type interfaceType)
        {
            var name = "Serialize";
            var methodBuilder = typeBuilder.DefineMethod(name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.Final,
                typeof(byte[]),
                new Type[] { typeof(object) }
                );
            var methodCode = methodBuilder.GetILGenerator();
            methodCode.ThrowException(typeof(NotImplementedException));
            typeBuilder.DefineMethodOverride(methodBuilder,
                GetInterfaceMethod(
                    interfaceType,
                    name,
                    new Type[] { typeof(object) }
                    ));
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
            methodCode.Emit(OpCodes.Brtrue_S, l1);
            methodCode.Emit(OpCodes.Ldtoken, interfaceType);
            methodCode.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
            methodCode.Emit(OpCodes.Ldarg_1);
            methodCode.Emit(OpCodes.Callvirt, typeof(Type).GetMethod("IsAssignableFrom"));
            methodCode.Emit(OpCodes.Br_S, l2);
            methodCode.MarkLabel(l1);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.MarkLabel(l2);
            methodCode.Emit(OpCodes.Stloc_0);
            methodCode.Emit(OpCodes.Br_S, exit);
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
            methodCode.Emit(OpCodes.Br_S, exit);
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
            getterCode.Emit(OpCodes.Br_S, label);
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
