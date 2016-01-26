using Altus.Suffūz.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class ILObservableTypeBuilder
    {
        private const string _prefix = "suff__observable__";
        private static AssemblyName _asmName = new AssemblyName() { Name = "Altus.Suffūz.Observables" };
        private static ModuleBuilder _modBuilder;
        private static AssemblyBuilder _asmBuilder;
        private static Dictionary<Type, Type> _types = new Dictionary<Type, Type>();

        static ILObservableTypeBuilder()
        {
            _asmBuilder = Thread.GetDomain().DefineDynamicAssembly(_asmName, AssemblyBuilderAccess.RunAndSave);
            _modBuilder = _asmBuilder.DefineDynamicModule(_asmName.Name + ".dll", true);
        }

        public static void Resolve(ResolveTypeEventArgs e)
        {
            var parts = e.TypeName.Split(',');
            if (parts.Length > 1
                && parts[1].Contains(_asmName.Name))
            {
                // it's an observable, but it hasn't been built yet, so build it
                var baseType = TypeHelper.GetType(parts[0].Replace(_prefix, "").Trim());
                e.ResolvedType = new ILObservableTypeBuilder().Build(baseType);
            }
        }

        public Type Build(Type type)
        {
            Type subType;
            lock(_types)
            {
                if (!_types.TryGetValue(type, out subType))
                {
                    subType = Create(type);
                    _types.Add(type, subType);
                }
            }
            return subType;
        }

        public void SaveAssembly()
        {
            _asmBuilder.Save(_asmName + ".dll");
        }

        private Type Create(Type type)
        {
            var interfaceType = typeof(IObservable<>).MakeGenericType(type);
            var className = GetTypeName(type);
            var typeBuilder = _modBuilder.DefineType(
                className,
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable | TypeAttributes.Sealed,
                type, // base type
                new Type[] { interfaceType } // interfaces
                );

            /*

            IObservable<T> --------------
            string GlobalKey { get; }
            T Instance { get; }
            ExclusiveLock SyncLock { get; }

            */

            var syncLockProp = ImplementProperty<ExclusiveLock>(typeBuilder, "SyncLock");
            var globalKeyProp = ImplementProperty<string>(typeBuilder, "GlobalKey");
            var publisherProp = ImplementProperty<IPublisher>(typeBuilder, "Publisher");
            var instanceProp = ImplementProperty(typeBuilder, "Instance", type);
            var explicitInstanceProp = ImplementInstanceProperty(typeBuilder, instanceProp);

            ImplementCtor(typeBuilder, 
                syncLockProp, 
                instanceProp, 
                globalKeyProp,
                publisherProp);

            ImplementPropertyProxies(typeBuilder, syncLockProp, globalKeyProp, publisherProp, instanceProp);
            ImplementMethodProxies(typeBuilder, syncLockProp, globalKeyProp, publisherProp);

            return typeBuilder.CreateType();
        }

        private void ImplementMethodProxies(TypeBuilder typeBuilder,
            PropertyInfo syncLock,
            PropertyInfo globalKey,
            PropertyInfo publisher)
        {
            foreach(var method in typeBuilder.BaseType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.IsVirtual && !method.IsSpecialName)
                    ImplementMethodProxy(typeBuilder, method, syncLock, globalKey, publisher);
            }
        }

        private MethodInfo ImplementMethodProxy(TypeBuilder typeBuilder, MethodInfo method,
            PropertyInfo syncLock,
            PropertyInfo globalKey,
            PropertyInfo publisher)
        {
            /*
            .method public hidebysig virtual instance int32 
            Hello(string message) cil managed
            {
              // Code size       154 (0x9a)
              .maxstack  8
              .locals init ([0] class ['Altus.Suffūz']'Altus.Suffūz.Observables'.RuntimeArgument[] args,
                       [1] class ['Altus.Suffūz']'Altus.Suffūz.Observables'.MethodCall`2<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32> beforeCall,
                       [2] class ['Altus.Suffūz']'Altus.Suffūz.Observables'.MethodCall`2<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32> afterCall,
                       [3] int32 V_3)
              IL_0000:  nop
              .try
              {
                IL_0001:  nop
                IL_0002:  ldarg.0
                IL_0003:  call       instance class ['Altus.Suffūz']'Altus.Suffūz.Threading'.ExclusiveLock 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_SyncLock()
                IL_0008:  callvirt   instance void ['Altus.Suffūz']'Altus.Suffūz.Threading'.ExclusiveLock::Enter()
                IL_000d:  nop
                IL_000e:  ldc.i4.1
                IL_000f:  newarr     ['Altus.Suffūz']'Altus.Suffūz.Observables'.RuntimeArgument
                IL_0014:  dup
                IL_0015:  ldc.i4.0
                IL_0016:  ldstr      "message"
                IL_001b:  ldarg.1
                IL_001c:  newobj     instance void ['Altus.Suffūz']'Altus.Suffūz.Observables'.RuntimeArgument::.ctor(string,
                                                                                                                       object)
                IL_0021:  stelem.ref
                IL_0022:  stloc.0
                IL_0023:  ldarg.0
                IL_0024:  call       instance string 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_GlobalKey()
                IL_0029:  ldc.i4.0
                IL_002a:  ldstr      "Hello"
                IL_002f:  ldtoken    'Altus.Suffūz.Observables.Tests.Observables'.StateClass
                IL_0034:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
                IL_0039:  ldarg.0
                IL_003a:  ldloc.0
                IL_003b:  newobj     instance void class ['Altus.Suffūz']'Altus.Suffūz.Observables'.MethodCall`2<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32>::.ctor(string,
                                                                                                                                                                                                valuetype ['Altus.Suffūz']'Altus.Suffūz.Observables'.OperationState,
                                                                                                                                                                                                string,
                                                                                                                                                                                                class [mscorlib]System.Type,
                                                                                                                                                                                                !0,
                                                                                                                                                                                                class ['Altus.Suffūz']'Altus.Suffūz.Observables'.RuntimeArgument[])
                IL_0040:  stloc.1
                IL_0041:  ldarg.0
                IL_0042:  call       instance class ['Altus.Suffūz']'Altus.Suffūz.Observables'.IPublisher 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_Publisher()
                IL_0047:  ldloc.1
                IL_0048:  callvirt   instance void ['Altus.Suffūz']'Altus.Suffūz.Observables'.IPublisher::Publish<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32>(class ['Altus.Suffūz']'Altus.Suffūz.Observables'.MethodCall`2<!!0,!!1>)
                IL_004d:  nop
                IL_004e:  ldarg.0
                IL_004f:  call       instance string 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_GlobalKey()
                IL_0054:  ldc.i4.0
                IL_0055:  ldstr      "Hello"
                IL_005a:  ldtoken    'Altus.Suffūz.Observables.Tests.Observables'.StateClass
                IL_005f:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
                IL_0064:  ldarg.0
                IL_0065:  ldloc.0
                IL_0066:  ldarg.0
                IL_0067:  ldarg.1
                IL_0068:  call       instance int32 'Altus.Suffūz.Observables.Tests.Observables'.StateClass::Hello(string)
                IL_006d:  newobj     instance void class ['Altus.Suffūz']'Altus.Suffūz.Observables'.MethodCall`2<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32>::.ctor(string,
                                                                                                                                                                                                valuetype ['Altus.Suffūz']'Altus.Suffūz.Observables'.OperationState,
                                                                                                                                                                                                string,
                                                                                                                                                                                                class [mscorlib]System.Type,
                                                                                                                                                                                                !0,
                                                                                                                                                                                                class ['Altus.Suffūz']'Altus.Suffūz.Observables'.RuntimeArgument[],
                                                                                                                                                                                                !1)
                IL_0072:  stloc.2
                IL_0073:  ldarg.0
                IL_0074:  call       instance class ['Altus.Suffūz']'Altus.Suffūz.Observables'.IPublisher 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_Publisher()
                IL_0079:  ldloc.1
                IL_007a:  callvirt   instance void ['Altus.Suffūz']'Altus.Suffūz.Observables'.IPublisher::Publish<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32>(class ['Altus.Suffūz']'Altus.Suffūz.Observables'.MethodCall`2<!!0,!!1>)
                IL_007f:  nop
                IL_0080:  ldloc.2
                IL_0081:  callvirt   instance !1 class ['Altus.Suffūz']'Altus.Suffūz.Observables'.MethodCall`2<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32>::get_ReturnValue()
                IL_0086:  stloc.3
                IL_0087:  leave.s    IL_0098
              }  // end .try
              finally
              {
                IL_0089:  nop
                IL_008a:  ldarg.0
                IL_008b:  call       instance class ['Altus.Suffūz']'Altus.Suffūz.Threading'.ExclusiveLock 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_SyncLock()
                IL_0090:  callvirt   instance void ['Altus.Suffūz']'Altus.Suffūz.Threading'.ExclusiveLock::Exit()
                IL_0095:  nop
                IL_0096:  nop
                IL_0097:  endfinally
              }  // end handler
              IL_0098:  ldloc.3
              IL_0099:  ret
            } // end of method Observable_StateClass::Hello

            */

            var methodBuilder = typeBuilder.DefineMethod(method.Name, method.Attributes | MethodAttributes.Final);
            methodBuilder.SetSignature(method.ReturnType, null, null, method.GetParameters().Select(p => p.ParameterType).ToArray(), null, null);

            var methodCode = methodBuilder.GetILGenerator();
            var hasReturn = method.ReturnType != typeof(void);
            var returnType = hasReturn ? method.ReturnType : typeof(object);
            var messageType = typeof(MethodCall<,>).MakeGenericType(typeBuilder.BaseType, returnType);
            var runtimeArgType = typeof(RuntimeArgument);
            var runtimeArgCtor = typeof(RuntimeArgument).GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(object) }, null);
            var runtimeArgs = methodCode.DeclareLocal(typeof(RuntimeArgument[])); // loc 0 
            var beforeCall = methodCode.DeclareLocal(messageType); // loc 1
            var afterCall = methodCode.DeclareLocal(messageType); // loc 2
            var exitLabel = methodCode.DefineLabel();
            var messageTypeCtorBefore = messageType.GetConstructors().Single(c => c.GetParameters().Length == 6);
            var messageTypeCtorAfter = messageType.GetConstructors().Single(c => c.GetParameters().Length == 7);
            var publish = publisher.PropertyType.GetMethods().Single(mi => mi.GetParameters()[0].ParameterType.GetGenericTypeDefinition().Equals(typeof(MethodCall<,>)))
                .MakeGenericMethod(typeBuilder.BaseType, returnType);
            if (hasReturn)
            {
                methodCode.DeclareLocal(method.ReturnType); // loc 3
            }

            methodCode.BeginExceptionBlock();

            // enter lock
            methodCode.Emit(OpCodes.Ldarg_0); // this
            methodCode.Emit(OpCodes.Call, syncLock.GetMethod);
            methodCode.Emit(OpCodes.Callvirt, typeof(ExclusiveLock).GetMethod("Enter"));
            // enter lock complete
            var parameterCount = method.GetParameters().Length;
            var parameters = method.GetParameters();
            methodCode.Emit(OpCodes.Ldc_I4, parameterCount);
            methodCode.Emit(OpCodes.Newarr, runtimeArgType);
            for(int p = 0; p < parameterCount; p++)
            {
                methodCode.Emit(OpCodes.Dup);
                methodCode.Emit(OpCodes.Ldc_I4, p);
                methodCode.Emit(OpCodes.Ldstr, parameters[p].Name);
                methodCode.Emit(OpCodes.Ldarg, p + 1);
                methodCode.Emit(OpCodes.Newobj, runtimeArgCtor);
                methodCode.Emit(OpCodes.Stelem_Ref);
            }
            methodCode.Emit(OpCodes.Stloc_0); // create RuntimeArgument array

            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, globalKey.GetGetMethod());
            methodCode.Emit(OpCodes.Ldc_I4, (int)OperationState.Before);
            methodCode.Emit(OpCodes.Ldstr, method.Name);
            methodCode.Emit(OpCodes.Ldtoken, typeBuilder.BaseType);
            methodCode.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Ldloc_0);
            methodCode.Emit(OpCodes.Newobj, messageTypeCtorBefore);
            methodCode.Emit(OpCodes.Stloc_1);
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, publisher.GetGetMethod());
            methodCode.Emit(OpCodes.Ldloc_1);
            methodCode.Emit(OpCodes.Callvirt, publish); // publish Before MethodCall message

            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, globalKey.GetGetMethod());
            methodCode.Emit(OpCodes.Ldc_I4, (int)OperationState.After);
            methodCode.Emit(OpCodes.Ldstr, method.Name);
            methodCode.Emit(OpCodes.Ldtoken, typeBuilder.BaseType);
            methodCode.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Ldloc_0);
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Ldarg_1);
            methodCode.Emit(OpCodes.Call, method); // call base class method
            methodCode.Emit(OpCodes.Newobj, messageTypeCtorAfter);
            methodCode.Emit(OpCodes.Stloc_2);
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, publisher.GetGetMethod());
            methodCode.Emit(OpCodes.Ldloc_2);
            methodCode.Emit(OpCodes.Callvirt, publish); // publish After MethodCall message

            if (hasReturn)
            {
                methodCode.Emit(OpCodes.Ldloc_2);
                methodCode.Emit(OpCodes.Callvirt, messageType.GetProperty("ReturnValue", BindingFlags.Public | BindingFlags.Instance).GetGetMethod());
                methodCode.Emit(OpCodes.Stloc_3);
            }

            methodCode.Emit(OpCodes.Leave, exitLabel);


            methodCode.BeginFinallyBlock();
            // exit lock
            methodCode.Emit(OpCodes.Ldarg_0); // this
            methodCode.Emit(OpCodes.Call, syncLock.GetMethod);
            methodCode.Emit(OpCodes.Callvirt, typeof(ExclusiveLock).GetMethod("Exit"));

            methodCode.EndExceptionBlock();
            methodCode.MarkLabel(exitLabel);
            if (hasReturn)
            {
                methodCode.Emit(OpCodes.Ldloc_3);
            }
            methodCode.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(methodBuilder, method);
            return methodBuilder;
        }

        private void ImplementPropertyProxies(TypeBuilder typeBuilder, PropertyInfo syncLock, PropertyInfo globalKey, PropertyInfo publisher, PropertyInfo instance)
        {
            var commutativeProperties = GetVirtualProperties<CommutativeEventAttribute>(typeBuilder.BaseType);
            foreach(var property in commutativeProperties)
            {
                ImplementCommutativeProperty(typeBuilder, property, syncLock, globalKey, publisher, instance);
            }

            var explicitProperties = GetVirtualProperties<ExplicitEventAttribute>(typeBuilder.BaseType);
            foreach (var property in explicitProperties)
            {
                ImplementExplicitProperty(typeBuilder, property, syncLock, globalKey, publisher, instance);
            }

            var sequentialProperties = GetVirtualProperties<SequentialEventAttribute>(typeBuilder.BaseType);
            foreach (var property in sequentialProperties)
            {
                throw new NotSupportedException("Sequential Properties are not supported in this version");
            }
        }

        private void ImplementCommutativeProperty(TypeBuilder typeBuilder, PropertyInfo property, 
            PropertyInfo syncLock,
            PropertyInfo globalKey, 
            PropertyInfo publisher, 
            PropertyInfo instance)
        {
            #region Getter Sample 
            /*
            IL
            .property instance int32 Size()
            {
              .get instance int32 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_Size()
              .set instance void 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::set_Size(int32)
            } // end of property Observable_StateClass::Size

            .method public hidebysig specialname virtual 
            instance int32  get_Size() cil managed
            {
              // Code size       12 (0xc)
              .maxstack  1
              .locals init ([0] int32 V_0)
              IL_0000:  nop
              IL_0001:  ldarg.0
              IL_0002:  call       instance int32 'Altus.Suffūz.Observables.Tests.Observables'.StateClass::get_Size()
              IL_0007:  stloc.0
              IL_0008:  br.s       IL_000a
              IL_000a:  ldloc.0
              IL_000b:  ret
            } // end of method Observable_StateClass::get_Size

            C#
            return base.Score;
            */
            #endregion

            #region IL Setter Sample
            /*
            IL
            .method public hidebysig specialname virtual 
            instance void  set_Size(int32 'value') cil managed
            {
              // Code size       143 (0x8f)
              .maxstack  9
              .locals init ([0] class ['Altus.Suffūz']'Altus.Suffūz.Observables'.PropertyUpdate`2<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32> beforeChange,
                       [1] class ['Altus.Suffūz']'Altus.Suffūz.Observables'.PropertyUpdate`2<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32> afterChange)
              IL_0000:  nop
              .try
              {
                IL_0001:  nop
                IL_0002:  ldarg.0
                IL_0003:  call       instance class ['Altus.Suffūz']'Altus.Suffūz.Threading'.ExclusiveLock 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_SyncLock()
                IL_0008:  callvirt   instance void ['Altus.Suffūz']'Altus.Suffūz.Threading'.ExclusiveLock::Enter()
                IL_000d:  nop
                IL_000e:  ldarg.0
                IL_000f:  call       instance string 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_GlobalKey()
                IL_0014:  ldc.i4.0
                IL_0015:  ldstr      "Size"
                IL_001a:  ldtoken    'Altus.Suffūz.Observables.Tests.Observables'.StateClass
                IL_001f:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
                IL_0024:  ldarg.0
                IL_0025:  ldc.i4.1   // Commutative
                IL_0026:  ldc.i4.2   // Additive
                IL_0027:  ldarg.0
                IL_0028:  call       instance int32 'Altus.Suffūz.Observables.Tests.Observables'.StateClass::get_Size()
                IL_002d:  ldarg.1
                IL_002e:  newobj     instance void class ['Altus.Suffūz']'Altus.Suffūz.Observables'.PropertyUpdate`2<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32>::.ctor(string,
                                                                                                                                                                                                    valuetype ['Altus.Suffūz']'Altus.Suffūz.Observables'.OperationState,
                                                                                                                                                                                                    string,
                                                                                                                                                                                                    class [mscorlib]System.Type,
                                                                                                                                                                                                    !0,
                                                                                                                                                                                                    valuetype ['Altus.Suffūz']'Altus.Suffūz.Observables'.EventClass,
                                                                                                                                                                                                    valuetype ['Altus.Suffūz']'Altus.Suffūz.Observables'.EventOrder,
                                                                                                                                                                                                    !1,
                                                                                                                                                                                                    !1)
                IL_0033:  stloc.0
                IL_0034:  ldarg.0
                IL_0035:  call       instance class ['Altus.Suffūz']'Altus.Suffūz.Observables'.IPublisher 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_Publisher()
                IL_003a:  ldloc.0
                IL_003b:  callvirt   instance void ['Altus.Suffūz']'Altus.Suffūz.Observables'.IPublisher::Publish<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32>(class ['Altus.Suffūz']'Altus.Suffūz.Observables'.PropertyUpdate`2<!!0,!!1>)
                IL_0040:  nop
                IL_0041:  ldarg.0
                IL_0042:  ldarg.1
                IL_0043:  call       instance void 'Altus.Suffūz.Observables.Tests.Observables'.StateClass::set_Size(int32)
                IL_0048:  nop
                IL_0049:  ldarg.0
                IL_004a:  call       instance string 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_GlobalKey()
                IL_004f:  ldc.i4.1
                IL_0050:  ldstr      "Size"
                IL_0055:  ldtoken    'Altus.Suffūz.Observables.Tests.Observables'.StateClass
                IL_005a:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
                IL_005f:  ldarg.0
                IL_0060:  ldc.i4.1
                IL_0061:  ldc.i4.2
                IL_0062:  ldarg.0
                IL_0063:  call       instance int32 'Altus.Suffūz.Observables.Tests.Observables'.StateClass::get_Size()
                IL_0068:  ldarg.1
                IL_0069:  newobj     instance void class ['Altus.Suffūz']'Altus.Suffūz.Observables'.PropertyUpdate`2<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32>::.ctor(string,
                                                                                                                                                                                                    valuetype ['Altus.Suffūz']'Altus.Suffūz.Observables'.OperationState,
                                                                                                                                                                                                    string,
                                                                                                                                                                                                    class [mscorlib]System.Type,
                                                                                                                                                                                                    !0,
                                                                                                                                                                                                    valuetype ['Altus.Suffūz']'Altus.Suffūz.Observables'.EventClass,
                                                                                                                                                                                                    valuetype ['Altus.Suffūz']'Altus.Suffūz.Observables'.EventOrder,
                                                                                                                                                                                                    !1,
                                                                                                                                                                                                    !1)
                IL_006e:  stloc.1
                IL_006f:  ldarg.0
                IL_0070:  call       instance class ['Altus.Suffūz']'Altus.Suffūz.Observables'.IPublisher 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_Publisher()
                IL_0075:  ldloc.1
                IL_0076:  callvirt   instance void ['Altus.Suffūz']'Altus.Suffūz.Observables'.IPublisher::Publish<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32>(class ['Altus.Suffūz']'Altus.Suffūz.Observables'.PropertyUpdate`2<!!0,!!1>)
                IL_007b:  nop
                IL_007c:  nop
                IL_007d:  leave.s    IL_008e
              }  // end .try
              finally
              {
                IL_007f:  nop
                IL_0080:  ldarg.0
                IL_0081:  call       instance class ['Altus.Suffūz']'Altus.Suffūz.Threading'.ExclusiveLock 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_SyncLock()
                IL_0086:  callvirt   instance void ['Altus.Suffūz']'Altus.Suffūz.Threading'.ExclusiveLock::Exit()
                IL_008b:  nop
                IL_008c:  nop
                IL_008d:  endfinally
              }  // end handler
              IL_008e:  ret
            } // end of method Observable_StateClass::set_Size
            */
            #endregion

            #region C# Setter Sample
            /*
            C#
            try
            {
                SyncLock.Enter();

                var beforeChange = new PropertyUpdate<StateClass, double>(this.GlobalKey,
                    OperationState.Before,
                    "Score",
                    typeof(StateClass),
                    this,
                    EventClass.Commutative,
                    EventOrder.Multiplicative,
                    base.Size,
                    value);
                Publisher.Publish(beforeChange);

                base.Score = value;

                var afterChange = new PropertyUpdate<StateClass, double>(this.GlobalKey,
                    OperationState.After,
                    "Score",
                    typeof(StateClass),
                    this,
                    EventClass.Commutative,
                    EventOrder.Multiplicative,
                    base.Size,
                    value);
                Publisher.Publish(afterChange);
            }
            finally
            {
                SyncLock.Exit();
            }
                
            */
            #endregion

            #region Simple Getter
            var getter = typeBuilder.DefineMethod(property.GetMethod.Name,
                property.GetMethod.Attributes | MethodAttributes.Final,
                property.PropertyType,
                Type.EmptyTypes);

            var getterCode = getter.GetILGenerator();
            getterCode.Emit(OpCodes.Ldarg_0);
            getterCode.Emit(OpCodes.Callvirt, instance.GetMethod); // get instance
            getterCode.Emit(OpCodes.Callvirt, property.GetMethod); // get value from instance
            getterCode.Emit(OpCodes.Ret);
            typeBuilder.DefineMethodOverride(getter, property.GetMethod);
            #endregion

            #region Setter with publications
            var setter = typeBuilder.DefineMethod(property.SetMethod.Name,
                property.SetMethod.Attributes | MethodAttributes.Final,
                null,
                new[] { property.PropertyType });

            var setterCode = setter.GetILGenerator();
            var updateType = typeof(PropertyUpdate<,>).MakeGenericType(typeBuilder.BaseType, property.PropertyType);
            var beforeChanged = setterCode.DeclareLocal(updateType); // before changed
            var afterChanged = setterCode.DeclareLocal(updateType); // after changed
            var baseValue = setterCode.DeclareLocal(property.PropertyType); // current value
            var exitLabel = setterCode.DefineLabel();
            var attrib = property.GetCustomAttribute<CommutativeEventAttribute>();
            var publish = publisher.PropertyType.GetMethods().Single(mi => mi.GetParameters()[0].ParameterType.GetGenericTypeDefinition().Equals(typeof(PropertyUpdate<,>)))
                .MakeGenericMethod(typeBuilder.BaseType, property.PropertyType);
            var updateTypeCtor = updateType.GetConstructors().Single(c => c.GetParameters().Length > 0);

            setterCode.BeginExceptionBlock(); // create try

            // enter lock
            setterCode.Emit(OpCodes.Ldarg_0); // this
            setterCode.Emit(OpCodes.Call, syncLock.GetMethod);
            setterCode.Emit(OpCodes.Callvirt, typeof(ExclusiveLock).GetMethod("Enter"));
            // enter lock complete


            // check equality
            setterCode.Emit(OpCodes.Ldarg_1); // value
            setterCode.Emit(OpCodes.Ldarg_0); // this
            setterCode.Emit(OpCodes.Call, property.GetGetMethod());
            setterCode.Emit(OpCodes.Stloc_2); // store it for later
            setterCode.Emit(OpCodes.Ldloc_2); // load it for equality check
            setterCode.Emit(OpCodes.Ceq);
            setterCode.Emit(OpCodes.Brtrue, exitLabel); // no change, so bail
            // check equality complete


            // publish Before Changed
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Call, globalKey.GetMethod);
            setterCode.Emit(OpCodes.Ldc_I4, (int)OperationState.Before); // before changed 
            setterCode.Emit(OpCodes.Ldstr, property.Name);
            setterCode.Emit(OpCodes.Ldtoken, typeBuilder.BaseType);
            setterCode.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static));
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Ldc_I4, (int)EventClass.Commutative);
            setterCode.Emit(OpCodes.Ldc_I4, (int)attrib.CommutativeEventType);
            setterCode.Emit(OpCodes.Ldloc_2);
            setterCode.Emit(OpCodes.Ldarg_1);
            setterCode.Emit(OpCodes.Newobj, updateTypeCtor);
            setterCode.Emit(OpCodes.Stloc_0);
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Call, publisher.GetMethod);
            setterCode.Emit(OpCodes.Ldloc_0);
            setterCode.Emit(OpCodes.Callvirt, publish);
            // publish complete



            // pass thru to base class to set value
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Ldarg_1);
            setterCode.Emit(OpCodes.Call, property.SetMethod);
            // pass thru complete

            // set value on Instance property
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Callvirt, instance.GetMethod);
            setterCode.Emit(OpCodes.Ldarg_1);
            setterCode.Emit(OpCodes.Callvirt, property.SetMethod);
            // set complete


            // publish After Changed
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Call, globalKey.GetMethod);
            setterCode.Emit(OpCodes.Ldc_I4, (int)OperationState.After); // before changed 
            setterCode.Emit(OpCodes.Ldstr, property.Name);
            setterCode.Emit(OpCodes.Ldtoken, typeBuilder.BaseType);
            setterCode.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static));
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Ldc_I4, (int)EventClass.Commutative);
            setterCode.Emit(OpCodes.Ldc_I4, (int)attrib.CommutativeEventType);
            setterCode.Emit(OpCodes.Ldloc_2);
            setterCode.Emit(OpCodes.Ldarg_1);
            setterCode.Emit(OpCodes.Newobj, updateTypeCtor);
            setterCode.Emit(OpCodes.Stloc_1);
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Call, publisher.GetMethod);
            setterCode.Emit(OpCodes.Ldloc_1);
            setterCode.Emit(OpCodes.Callvirt, publish);
            // publish complete

            setterCode.BeginFinallyBlock();
            // exit lock
            setterCode.Emit(OpCodes.Ldarg_0); // this
            setterCode.Emit(OpCodes.Call, syncLock.GetMethod);
            setterCode.Emit(OpCodes.Callvirt, typeof(ExclusiveLock).GetMethod("Exit"));
            // exit complete
            setterCode.EndExceptionBlock();
            setterCode.MarkLabel(exitLabel);
            setterCode.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(setter, property.SetMethod);
            #endregion
        }

        private void ImplementExplicitProperty(TypeBuilder typeBuilder, PropertyInfo property,
           PropertyInfo syncLock,
           PropertyInfo globalKey,
           PropertyInfo publisher, 
           PropertyInfo instance)
        {
            #region Getter Sample 
            /*
            IL
            .property instance int32 Size()
            {
              .get instance int32 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_Size()
              .set instance void 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::set_Size(int32)
            } // end of property Observable_StateClass::Size

            .method public hidebysig specialname virtual 
            instance int32  get_Size() cil managed
            {
              // Code size       12 (0xc)
              .maxstack  1
              .locals init ([0] int32 V_0)
              IL_0000:  nop
              IL_0001:  ldarg.0
              IL_0002:  call       instance int32 'Altus.Suffūz.Observables.Tests.Observables'.StateClass::get_Size()
              IL_0007:  stloc.0
              IL_0008:  br.s       IL_000a
              IL_000a:  ldloc.0
              IL_000b:  ret
            } // end of method Observable_StateClass::get_Size

            C#
            return Instance.Score;
            */
            #endregion

            #region IL Setter Sample
            /*
            IL
            .method public hidebysig specialname virtual 
            instance void  set_Size(int32 'value') cil managed
            {
              // Code size       143 (0x8f)
              .maxstack  9
              .locals init ([0] class ['Altus.Suffūz']'Altus.Suffūz.Observables'.PropertyUpdate`2<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32> beforeChange,
                       [1] class ['Altus.Suffūz']'Altus.Suffūz.Observables'.PropertyUpdate`2<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32> afterChange)
              IL_0000:  nop
              .try
              {
                IL_0001:  nop
                IL_0002:  ldarg.0
                IL_0003:  call       instance class ['Altus.Suffūz']'Altus.Suffūz.Threading'.ExclusiveLock 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_SyncLock()
                IL_0008:  callvirt   instance void ['Altus.Suffūz']'Altus.Suffūz.Threading'.ExclusiveLock::Enter()
                IL_000d:  nop
                IL_0002:  ldarg.1
                IL_0003:  ldarg.0
                IL_0004:  call       instance int32 'Altus.Suffūz.Observables.Tests.Observables'.StateClass::get_Size()
                IL_0009:  ceq
                IL_000b:  stloc.2
                IL_000c:  ldloc.2
                IL_000d:  brfalse.s  IL_0014
                IL_000f:  leave      IL_00a0
                IL_000e:  ldarg.0
                IL_000f:  call       instance string 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_GlobalKey()
                IL_0014:  ldc.i4.0
                IL_0015:  ldstr      "Size"
                IL_001a:  ldtoken    'Altus.Suffūz.Observables.Tests.Observables'.StateClass
                IL_001f:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
                IL_0024:  ldarg.0
                IL_0025:  ldc.i4.1   // Commutative
                IL_0026:  ldc.i4.2   // Additive
                IL_0027:  ldarg.0
                IL_0028:  call       instance int32 'Altus.Suffūz.Observables.Tests.Observables'.StateClass::get_Size()
                IL_002d:  ldarg.1
                IL_002e:  newobj     instance void class ['Altus.Suffūz']'Altus.Suffūz.Observables'.PropertyUpdate`2<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32>::.ctor(string,
                                                                                                                                                                                                    valuetype ['Altus.Suffūz']'Altus.Suffūz.Observables'.OperationState,
                                                                                                                                                                                                    string,
                                                                                                                                                                                                    class [mscorlib]System.Type,
                                                                                                                                                                                                    !0,
                                                                                                                                                                                                    valuetype ['Altus.Suffūz']'Altus.Suffūz.Observables'.EventClass,
                                                                                                                                                                                                    valuetype ['Altus.Suffūz']'Altus.Suffūz.Observables'.EventOrder,
                                                                                                                                                                                                    !1,
                                                                                                                                                                                                    !1)
                IL_0033:  stloc.0
                IL_0034:  ldarg.0
                IL_0035:  call       instance class ['Altus.Suffūz']'Altus.Suffūz.Observables'.IPublisher 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_Publisher()
                IL_003a:  ldloc.0
                IL_003b:  callvirt   instance void ['Altus.Suffūz']'Altus.Suffūz.Observables'.IPublisher::Publish<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32>(class ['Altus.Suffūz']'Altus.Suffūz.Observables'.PropertyUpdate`2<!!0,!!1>)
                IL_0040:  nop
                IL_0041:  ldarg.0
                IL_0042:  ldarg.1
                IL_0043:  call       instance void 'Altus.Suffūz.Observables.Tests.Observables'.StateClass::set_Size(int32)
                IL_0048:  nop
                IL_0049:  ldarg.0
                IL_004a:  call       instance string 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_GlobalKey()
                IL_004f:  ldc.i4.1
                IL_0050:  ldstr      "Size"
                IL_0055:  ldtoken    'Altus.Suffūz.Observables.Tests.Observables'.StateClass
                IL_005a:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
                IL_005f:  ldarg.0
                IL_0060:  ldc.i4.1
                IL_0061:  ldc.i4.2
                IL_0062:  ldarg.0
                IL_0063:  call       instance int32 'Altus.Suffūz.Observables.Tests.Observables'.StateClass::get_Size()
                IL_0068:  ldarg.1
                IL_0069:  newobj     instance void class ['Altus.Suffūz']'Altus.Suffūz.Observables'.PropertyUpdate`2<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32>::.ctor(string,
                                                                                                                                                                                                    valuetype ['Altus.Suffūz']'Altus.Suffūz.Observables'.OperationState,
                                                                                                                                                                                                    string,
                                                                                                                                                                                                    class [mscorlib]System.Type,
                                                                                                                                                                                                    !0,
                                                                                                                                                                                                    valuetype ['Altus.Suffūz']'Altus.Suffūz.Observables'.EventClass,
                                                                                                                                                                                                    valuetype ['Altus.Suffūz']'Altus.Suffūz.Observables'.EventOrder,
                                                                                                                                                                                                    !1,
                                                                                                                                                                                                    !1)
                IL_006e:  stloc.1
                IL_006f:  ldarg.0
                IL_0070:  call       instance class ['Altus.Suffūz']'Altus.Suffūz.Observables'.IPublisher 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_Publisher()
                IL_0075:  ldloc.1
                IL_0076:  callvirt   instance void ['Altus.Suffūz']'Altus.Suffūz.Observables'.IPublisher::Publish<class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass,int32>(class ['Altus.Suffūz']'Altus.Suffūz.Observables'.PropertyUpdate`2<!!0,!!1>)
                IL_007b:  nop
                IL_007c:  nop
                IL_007d:  leave.s    IL_008e
              }  // end .try
              finally
              {
                IL_007f:  nop
                IL_0080:  ldarg.0
                IL_0081:  call       instance class ['Altus.Suffūz']'Altus.Suffūz.Threading'.ExclusiveLock 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_SyncLock()
                IL_0086:  callvirt   instance void ['Altus.Suffūz']'Altus.Suffūz.Threading'.ExclusiveLock::Exit()
                IL_008b:  nop
                IL_008c:  nop
                IL_008d:  endfinally
              }  // end handler
              IL_008e:  ret
            } // end of method Observable_StateClass::set_Size
            */
            #endregion

            #region C# Setter Sample
            /*
            C#
            try
            {
                if (base.Score == value) return;

                SyncLock.Enter();

                var beforeChange = new PropertyUpdate<StateClass, double>(this.GlobalKey,
                    OperationState.Before,
                    "Score",
                    typeof(StateClass),
                    this,
                    EventClass.Commutative,
                    EventOrder.Multiplicative,
                    base.Size,
                    value);
                Publisher.Publish(beforeChange);

                base.Score = value;

                var afterChange = new PropertyUpdate<StateClass, double>(this.GlobalKey,
                    OperationState.After,
                    "Score",
                    typeof(StateClass),
                    this,
                    EventClass.Commutative,
                    EventOrder.Multiplicative,
                    base.Size,
                    value);
                Publisher.Publish(afterChange);
            }
            finally
            {
                SyncLock.Exit();
            }
                
            */
            #endregion

            #region Simple Getter
            var getter = typeBuilder.DefineMethod(property.GetMethod.Name,
                property.GetMethod.Attributes,
                property.PropertyType,
                Type.EmptyTypes);

            var getterCode = getter.GetILGenerator();
            getterCode.Emit(OpCodes.Ldarg_0);
            getterCode.Emit(OpCodes.Callvirt, instance.GetMethod); // get instance
            getterCode.Emit(OpCodes.Callvirt, property.GetMethod); // get value from instance
            getterCode.Emit(OpCodes.Ret);
            typeBuilder.DefineMethodOverride(getter, property.GetMethod);
            #endregion

            #region Setter with publications
            var setter = typeBuilder.DefineMethod(property.SetMethod.Name,
                property.SetMethod.Attributes,
                null,
                new[] { property.PropertyType });

            var setterCode = setter.GetILGenerator();
            var updateType = typeof(PropertyUpdate<,>).MakeGenericType(typeBuilder.BaseType, property.PropertyType);
            var beforeChanged = setterCode.DeclareLocal(updateType); // before changed
            var afterChanged = setterCode.DeclareLocal(updateType); // after changed
            var baseValue = setterCode.DeclareLocal(property.PropertyType); // current value
            var exitLabel = setterCode.DefineLabel();
            var attrib = property.GetCustomAttribute<ExplicitEventAttribute>();
            var publish = publisher.PropertyType.GetMethods().Single(mi => mi.GetParameters()[0].ParameterType.GetGenericTypeDefinition().Equals(typeof(PropertyUpdate<,>)))
                .MakeGenericMethod(typeBuilder.BaseType, property.PropertyType);
            var updateTypeCtor = updateType.GetConstructors().Single(c => c.GetParameters().Length > 0);

            setterCode.BeginExceptionBlock(); // create try

            // enter lock
            setterCode.Emit(OpCodes.Ldarg_0); // this
            setterCode.Emit(OpCodes.Call, syncLock.GetMethod);
            setterCode.Emit(OpCodes.Callvirt, typeof(ExclusiveLock).GetMethod("Enter"));
            // enter lock complete

            // check equality
            setterCode.Emit(OpCodes.Ldarg_1); // value
            setterCode.Emit(OpCodes.Ldarg_0); // this
            setterCode.Emit(OpCodes.Call, property.GetGetMethod());
            setterCode.Emit(OpCodes.Stloc_2); // store it for later
            setterCode.Emit(OpCodes.Ldloc_2); // load it for equality check
            setterCode.Emit(OpCodes.Ceq);
            setterCode.Emit(OpCodes.Brtrue, exitLabel); // no change, so bail
            // check equality complete

            // publish Before Changed
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Call, globalKey.GetMethod);
            setterCode.Emit(OpCodes.Ldc_I4, (int)OperationState.Before); // before changed 
            setterCode.Emit(OpCodes.Ldstr, property.Name);
            setterCode.Emit(OpCodes.Ldtoken, typeBuilder.BaseType);
            setterCode.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static));
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Ldc_I4, (int)EventClass.Explicit);
            setterCode.Emit(OpCodes.Ldc_I4, (int)attrib.OrderedEventType);
            setterCode.Emit(OpCodes.Ldloc_2);
            setterCode.Emit(OpCodes.Ldarg_1);
            setterCode.Emit(OpCodes.Newobj, updateTypeCtor);
            setterCode.Emit(OpCodes.Stloc_0);
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Call, publisher.GetMethod);
            setterCode.Emit(OpCodes.Ldloc_0);
            setterCode.Emit(OpCodes.Callvirt, publish);
            // publish complete



            // pass thru to base class to set value
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Ldarg_1);
            setterCode.Emit(OpCodes.Call, property.SetMethod);
            // pass thru complete

            // set value on Instance property
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Callvirt, instance.GetMethod);
            setterCode.Emit(OpCodes.Ldarg_1);
            setterCode.Emit(OpCodes.Callvirt, property.SetMethod);
            // set complete


            // publish After Changed
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Call, globalKey.GetMethod);
            setterCode.Emit(OpCodes.Ldc_I4, (int)OperationState.After); // before changed 
            setterCode.Emit(OpCodes.Ldstr, property.Name);
            setterCode.Emit(OpCodes.Ldtoken, typeBuilder.BaseType);
            setterCode.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static));
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Ldc_I4, (int)EventClass.Explicit);
            setterCode.Emit(OpCodes.Ldc_I4, (int)attrib.OrderedEventType);
            setterCode.Emit(OpCodes.Ldloc_2);
            setterCode.Emit(OpCodes.Ldarg_1);
            setterCode.Emit(OpCodes.Newobj, updateTypeCtor);
            setterCode.Emit(OpCodes.Stloc_1);
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Call, publisher.GetMethod);
            setterCode.Emit(OpCodes.Ldloc_1);
            setterCode.Emit(OpCodes.Callvirt, publish);
            // publish complete


            //setterCode.Emit(OpCodes.Leave_S, exitLabel);

            setterCode.BeginFinallyBlock();
            // exit lock
            setterCode.Emit(OpCodes.Ldarg_0); // this
            setterCode.Emit(OpCodes.Call, syncLock.GetMethod);
            setterCode.Emit(OpCodes.Callvirt, typeof(ExclusiveLock).GetMethod("Exit"));
            // exit complete
            setterCode.EndExceptionBlock();
            //setterCode.Emit(OpCodes.Endfinally);
            setterCode.MarkLabel(exitLabel);
            setterCode.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(setter, property.SetMethod);
            #endregion
        }


        private IEnumerable<MethodInfo> GetVirtualMethods<T>(Type type) where T : Attribute
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                       .Where(mi => mi.GetCustomAttribute<T>() != null && mi.IsVirtual);
        }

        private IEnumerable<PropertyInfo> GetVirtualProperties<T>(Type type) where T : Attribute
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                       .Where(mi => ((mi is PropertyInfo) && ((PropertyInfo)mi).CanRead && ((PropertyInfo)mi).CanWrite)
                                    && mi.GetCustomAttribute<T>() != null
                                    && mi.GetMethod.IsVirtual
                                    && mi.SetMethod.IsVirtual);
        }

        private ConstructorInfo ImplementCtor(TypeBuilder typeBuilder, 
            PropertyInfo exclusiveLockProp, 
            PropertyInfo instanceProp, 
            PropertyInfo globalKeyProp,
            PropertyInfo publisherProp)
        {
            /*
            .method public hidebysig specialname rtspecialname 
            instance void  .ctor(class ['Altus.Suffūz']'Altus.Suffūz.Observables'.IPublisher publisher,
                                 class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass 'instance',
                                 string globalKey) cil managed
            {
              // Code size       46 (0x2e)
              .maxstack  8
              IL_0000:  ldarg.0
              IL_0001:  call       instance void 'Altus.Suffūz.Observables.Tests.Observables'.StateClass::.ctor()
              IL_0006:  nop
              IL_0007:  nop
              IL_0008:  ldarg.0
              IL_0009:  ldarg.3
              IL_000a:  newobj     instance void ['Altus.Suffūz']'Altus.Suffūz.Threading'.ExclusiveLock::.ctor(string)
              IL_000f:  call       instance void 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::set_SyncLock(class ['Altus.Suffūz']'Altus.Suffūz.Threading'.ExclusiveLock)
              IL_0014:  nop
              IL_0015:  ldarg.0
              IL_0016:  ldarg.3
              IL_0017:  call       instance void 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::set_GlobalKey(string)
              IL_001c:  nop
              IL_001d:  ldarg.0
              IL_001e:  ldarg.2
              IL_001f:  call       instance void 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::set_Instance(class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass)
              IL_0024:  nop
              IL_0025:  ldarg.0
              IL_0026:  ldarg.1
              IL_0027:  call       instance void 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::set_Publisher(class ['Altus.Suffūz']'Altus.Suffūz.Observables'.IPublisher)
              IL_002c:  nop
              IL_002d:  ret
            } // end of method Observable_StateClass::.ctor
            */

            var ctorBuilder = typeBuilder.DefineConstructor(
               MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
               CallingConventions.Standard,
               new Type[] { typeof(IPublisher), typeBuilder.BaseType, typeof(string) });
            var baseType = typeBuilder.BaseType;
            var ctorCode = ctorBuilder.GetILGenerator();

            ctorCode.Emit(OpCodes.Ldarg_0);
            ctorCode.Emit(OpCodes.Call, baseType.GetConstructor(new Type[0]));
            ctorCode.Emit(OpCodes.Ldarg_0);
            ctorCode.Emit(OpCodes.Ldarg_3); // global key arg
            ctorCode.Emit(OpCodes.Newobj, typeof(ExclusiveLock).GetConstructors().First()); // there's only one
            ctorCode.Emit(OpCodes.Call, exclusiveLockProp.GetSetMethod(true)); // create a new lock, using the global key
            ctorCode.Emit(OpCodes.Ldarg_0);
            ctorCode.Emit(OpCodes.Ldarg_3); // global key arg
            ctorCode.Emit(OpCodes.Call, globalKeyProp.GetSetMethod(true)); // set the global key property
            ctorCode.Emit(OpCodes.Ldarg_0);
            ctorCode.Emit(OpCodes.Ldarg_2); // instance arg
            ctorCode.Emit(OpCodes.Call, instanceProp.GetSetMethod(true)); // set instance property
            ctorCode.Emit(OpCodes.Ldarg_0);
            ctorCode.Emit(OpCodes.Ldarg_1); // publisher arg
            ctorCode.Emit(OpCodes.Call, publisherProp.GetSetMethod(true)); // set publisher
            ctorCode.Emit(OpCodes.Ret); // return self
            return ctorBuilder;
        }

        private PropertyInfo ImplementProperty<T>(TypeBuilder typeBuilder, string propertyName, MethodAttributes setterAttributes = MethodAttributes.Private)
        {
            return ImplementProperty(typeBuilder, propertyName, typeof(T), setterAttributes);
        }

        private PropertyInfo ImplementProperty(TypeBuilder typeBuilder, string propertyName, Type propertyType, MethodAttributes setterAttributes = MethodAttributes.Private)
        {
            var propType = propertyType;
            var piName = propertyName;
            var backingField = typeBuilder.DefineField("_" + piName.ToLower(), propType, FieldAttributes.Public);

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
            getterCode.Emit(OpCodes.Ldfld, backingField);
            getterCode.Emit(OpCodes.Ret);
            property.SetGetMethod(getter);

            var setter = typeBuilder.DefineMethod("set_" + piName,
                setterAttributes
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
            setterCode.Emit(OpCodes.Stfld, backingField);
            setterCode.Emit(OpCodes.Ret);
            property.SetSetMethod(setter);

            return property;
        }

        private MethodInfo ImplementInstanceProperty(TypeBuilder typeBuilder, PropertyInfo instanceProp)
        {
            /*
            .method private hidebysig newslot specialname virtual final 
                    instance object  'Altus.Suffūz.Observables.IObservable.get_Instance'() cil managed
            {
              .override ['Altus.Suffūz']'Altus.Suffūz.Observables'.IObservable::get_Instance
              // Code size       12 (0xc)
              .maxstack  1
              .locals init ([0] object V_0)
              IL_0000:  nop
              IL_0001:  ldarg.0
              IL_0002:  call       instance class 'Altus.Suffūz.Observables.Tests.Observables'.StateClass 'Altus.Suffūz.Observables.Tests.Observables'.Observable_StateClass::get_Instance()
              IL_0007:  stloc.0
              IL_0008:  br.s       IL_000a
              IL_000a:  ldloc.0
              IL_000b:  ret
            } // end of method Observable_StateClass::'Altus.Suffūz.Observables.IObservable.get_Instance'
            */

            var getter = typeBuilder.DefineMethod("IObservable.get_Instance",
                MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final,
                typeof(object),
                Type.EmptyTypes);
            var getterCode = getter.GetILGenerator();
            getterCode.Emit(OpCodes.Ldarg_0);
            getterCode.Emit(OpCodes.Call, instanceProp.GetGetMethod());
            getterCode.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(getter, typeof(IObservable).GetProperty("Instance").GetGetMethod());

            return getter;
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
                var genType = type.GetGenericTypeDefinition().Name.Replace("<", "").Replace(">", "").Replace(",", "").Replace("`", "");
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
    }
}
