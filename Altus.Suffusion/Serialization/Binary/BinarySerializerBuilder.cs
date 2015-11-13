using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Serialization.Binary
{
    public class BinarySerializerBuilder : IComparer<MemberInfo>, IBinarySerializerBuilder
    {
        static Dictionary<Type, Type> _serializers = new Dictionary<Type, Type>();

        /// <summary>
        /// Creates a serializer instance for the specified data type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public ISerializer<T> CreateSerializerType<T>()
        {
            return (ISerializer<T>)CreateSerializerType(typeof(T));
        }

        /// <summary>
        /// Creates a serializer instance for the specified data type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public ISerializer CreateSerializerType(Type type)
        {
            if (type != null)
            {
                lock (_serializers)
                {
                    if (_serializers.ContainsKey(type)) return (ISerializer)Activator.CreateInstance(_serializers[type]);
                }
                string template = GetTemplate();
                template = template.Replace("<TypeName>", type.Name);
                StringBuilder sbReader = new StringBuilder("");
                StringBuilder sbWriter = new StringBuilder("");
                MemberInfo[] members = type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                PropertyInfo[] props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                List<MemberInfo> serializedMembers = new List<MemberInfo>();
                serializedMembers.AddRange(fields.Where(fi => fi.GetCustomAttributes(typeof(BinarySerializableAttribute), true).Length > 0));
                serializedMembers.AddRange(props.Where(pi => pi.GetCustomAttributes(typeof(BinarySerializableAttribute), true).Length > 0));

                serializedMembers.Sort(this); // a consistent serialization order is required for backward-forward compatibility

                foreach (MemberInfo mi in serializedMembers)
                {
                    if (mi.MemberType == MemberTypes.Field)
                    {
                        FieldInfo fi = (FieldInfo)mi;
                        if (fi.IsFamily || fi.IsPublic)
                        {
                            object[] serializable = fi.GetCustomAttributes(typeof(BinarySerializableAttribute), true);
                            if (serializable != null && serializable.Length > 0)
                            {
                                if (((BinarySerializableAttribute)serializable[0]).SerializationType == null)
                                    ((BinarySerializableAttribute)serializable[0]).SerializationType = fi.FieldType;
                                AddReader(sbReader, mi.Name, fi.FieldType, (BinarySerializableAttribute)serializable[0]);
                                AddWriter(sbWriter, mi.Name, fi.FieldType, (BinarySerializableAttribute)serializable[0]);
                            }
                        }
                    }
                    else if (mi.MemberType == MemberTypes.Property)
                    {
                        PropertyInfo pi = (PropertyInfo)mi;
                        MethodInfo gettor = pi.GetGetMethod(true);
                        if (gettor != null
                            && (gettor.IsFamily || gettor.IsPublic))
                        {
                            MethodInfo settor = pi.GetSetMethod(true);
                            object[] serializable = pi.GetCustomAttributes(typeof(BinarySerializableAttribute), true);
                            if ((settor != null && (settor.IsFamily || settor.IsPublic))
                                || (gettor.ReturnType.IsGenericType && gettor.ReturnType.GetGenericTypeDefinition().Equals(typeof(IList<>)))
                                || (serializable != null && serializable.Length > 0 && ((BinarySerializableAttribute[])serializable)[0].SerializationType != null && ((BinarySerializableAttribute[])serializable)[0].SerializationType.Equals(typeof(IList<>))))
                            {
                                if (serializable != null && serializable.Length > 0)
                                {
                                    if (((BinarySerializableAttribute)serializable[0]).SerializationType == null)
                                        ((BinarySerializableAttribute)serializable[0]).SerializationType = gettor.ReturnType;
                                    AddReader(sbReader, mi.Name, settor.GetParameters()[0].ParameterType, (BinarySerializableAttribute)serializable[0]);
                                    AddWriter(sbWriter, mi.Name, gettor.ReturnType, (BinarySerializableAttribute)serializable[0]);
                                }
                            }
                        }
                    }
                }
                //if (type.IsSubclassOf(typeof(AbstractEntity)))
                //{
                //    AddLastBlockReader(sbReader);
                //    AddLastBlockWriter(sbWriter);
                //}
                template = template.Replace("<WriteValues>", sbWriter.ToString());
                template = template.Replace("<ReadValues>", sbReader.ToString());
                template = template.Replace("<Namespace>", type.Namespace);
                template = template.Replace("<Using>", "using " + type.Namespace + ";");
                template = template.Replace("<QualifiedTypeName>", type.FullName);
                if (type.GetProperty("Name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) == null)
                {
                    template = template.Replace("<NameProperty>", @"
        public string Name
        {
            get;
            private set;
        }");
                }
                else
                {
                    template = template.Replace("<NameProperty>", "");
                }

                lock (_serializers)
                {
                    _serializers.Add(type, CompileSerializer(template, type));
                }
                return (ISerializer)Activator.CreateInstance(_serializers[type]);
            }
            return null;
        }

        public int Compare(MemberInfo x, MemberInfo y)
        {
            BinarySerializableAttribute xA = ((BinarySerializableAttribute[])x.GetCustomAttributes(typeof(BinarySerializableAttribute), true))[0];
            BinarySerializableAttribute yA = ((BinarySerializableAttribute[])y.GetCustomAttributes(typeof(BinarySerializableAttribute), true))[0];
            return xA.SortOrder.CompareTo(yA.SortOrder);
        }

        private Type CompileSerializer(string template, Type baseType)
        {
            string directoryName = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            // that's all we need to do with the template, now just compile it and return the new type
            CodeDomProvider codeProvider = new CSharpCodeProvider();
            CompilerParameters compilerParams = new CompilerParameters();
            // setup the basic compiler options
            compilerParams.CompilerOptions = "/target:library";
            compilerParams.GenerateExecutable = false;


            directoryName = Path.Combine(directoryName, ConfigurationManager.AppSettings["tempDir"], Guid.NewGuid().ToString());
            string path = Path.Combine(directoryName, baseType.FullName + ".serializer.dll");

            Directory.CreateDirectory(directoryName);

            if (File.Exists(path))
                File.Delete(path);
            compilerParams.OutputAssembly = path;
            //#if(DEBUG)
            compilerParams.GenerateInMemory = false;
            compilerParams.IncludeDebugInformation = true;
            compilerParams.TempFiles = new TempFileCollection(directoryName, bool.Parse(ConfigurationManager.AppSettings["debugInjections"])); // change to true to load in VS debugger
            //#else
            //            compilerParams.GenerateInMemory = true;
            //            compilerParams.IncludeDebugInformation = false;
            //            compilerParams.TempFiles = new TempFileCollection(directoryName, false);
            //#endif

            // add references to external assemblies
            var runtimeAsm = Assembly.Load("System.Runtime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

            compilerParams.ReferencedAssemblies.Add("mscorlib.dll");
            compilerParams.ReferencedAssemblies.Add("system.dll");
            compilerParams.ReferencedAssemblies.Add("system.collections.dll");
            compilerParams.ReferencedAssemblies.Add("system.core.dll");
            compilerParams.ReferencedAssemblies.Add("system.windows.forms.dll");
            compilerParams.ReferencedAssemblies.Add(new Uri(runtimeAsm.CodeBase).LocalPath);
            compilerParams.ReferencedAssemblies.Add(new Uri(typeof(BinarySerializerBuilder).Assembly.CodeBase).LocalPath);
            Type t = baseType;
            while (t != null
                //&& !string.IsNullOrEmpty(t.Assembly.Location)
                && !t.Assembly.GetName().Name.Equals("system", StringComparison.InvariantCultureIgnoreCase)
                && !t.Assembly.GetName().Name.Equals("mscorlib", StringComparison.InvariantCultureIgnoreCase)
                && !t.Assembly.GetName().Name.Equals("system.core", StringComparison.InvariantCultureIgnoreCase)
                && !t.Assembly.GetName().Name.Equals("system.windows.forms", StringComparison.InvariantCultureIgnoreCase)
                && !t.Assembly.GetName().Name.Equals(typeof(BinarySerializerBuilder).Assembly.GetName().Name, StringComparison.InvariantCultureIgnoreCase)
                && !compilerParams.ReferencedAssemblies.Contains(t.Assembly.CodeBase))
            {
                compilerParams.ReferencedAssemblies.Add(new System.Uri(t.Assembly.CodeBase).LocalPath);
                t = t.BaseType;
            }



            // compile the source code
            CompilerResults results = codeProvider.CompileAssemblyFromSource(compilerParams, template);
            if (results.Errors.HasErrors)
            {
                // log failure
                StringBuilder sb = new StringBuilder("");
                for (int i = 0; i < results.Errors.Count; i++)
                {
                    // LogError(results.Errors[i].ErrorText);
                    sb.AppendLine(results.Errors[i].ErrorText);
                }
                throw (new InvalidProgramException("The binary serializer could not be compiled due to the following error(s):\r\n" + sb.ToString()));
            }
            else
            {
                Assembly dynamicAssembly = results.CompiledAssembly;

                // now get the new type
                return dynamicAssembly.GetType(baseType.Namespace + ".BinarySerializer_" + baseType.Name);
            }
        }

        private void AddLastBlockReader(StringBuilder sbReader)
        {
            sbReader.Append("\t\tif (br.BaseStream.Length > br.BaseStream.Position + 1){\r\n");
            sbReader.Append("\t\t\ttyped.AdditionalPayload = br.ReadBytes((int)(br.BaseStream.Length - br.BaseStream.Position));}\r\n");
        }

        private void AddReader(StringBuilder sbReader, string memberName, Type memberType, BinarySerializableAttribute bsa)
        {
            sbReader.AppendLine("\t\tif (br.BaseStream.Position >= br.BaseStream.Length) return typed;");
            if (memberType.Equals(typeof(byte[])))
            {
                sbReader.Append(string.Format("\t\ttyped.{0} = br.ReadBytes(br.ReadInt32());\r\n", memberName));
            }
            else if (memberType.IsArray)
            {
                Type arrayType = memberType.GetElementType();
                if (arrayType.IsValueType
                    || arrayType.Equals(typeof(string)))
                {
                    sbReader.Append("\t\tint count = br.ReadInt32();\r\n");
                    sbReader.Append(String.Format("\t\t{0}[] vals = new {0}[count];\r\n", arrayType.Name));
                    sbReader.Append("\t\tfor(int i = 0; i < count; i++)\r\n");
                    sbReader.Append("\t\t{\r\n");
                    sbReader.Append(String.Format("\t\t\tvals[i] = br.Read{0}();\r\n", arrayType.Name));
                    sbReader.Append("\t\t}\r\n");
                    sbReader.Append(String.Format("\t\ttyped.{0} = vals;\r\n", memberName));
                }
                else
                {
                    sbReader.Append("\t\tint count = br.ReadInt32();\r\n");
                    sbReader.Append(String.Format("\t\t{0}[] vals = new {0}[count];\r\n", arrayType.Name));
                    sbReader.Append("\t\tfor(int i = 0; i < count; i++)\r\n");
                    sbReader.Append("\t\t{\r\n");
                    sbReader.Append(string.Format("\t\t\ttyped.{0} = ({1})this.DeserializeType(br);\r\n", memberName, memberType.FullName));
                    sbReader.Append("\t\t}\r\n");
                    sbReader.Append(String.Format("\t\ttyped.{0} = vals;\r\n", memberName));
                }
            }
            else if (bsa.SerializationType.Equals(typeof(IEnumerable<>)))
            {
                Type elementType = memberType.GetGenericArguments()[0];
                memberType = bsa.SerializationType;
                sbReader.Append(string.Format("\t\tint {0}_count = br.ReadInt32();\r\n", memberName));
                sbReader.Append(string.Format("\t\t{0}[] array = new {0}[{1}_count];\r\n", elementType.FullName, memberName));
                sbReader.Append(string.Format("\t\tfor(int i = 0; i < {0}_count; i++)\r\n", memberName));
                sbReader.Append("\t\t{\r\n");
                sbReader.Append(String.Format("\t\t\tarray[i] = ({0})this.DeserializeType(br);\r\n", elementType.FullName, memberName));
                sbReader.Append("\t\t}\r\n");
                sbReader.Append(string.Format("\t\ttyped.{0} = array;\r\n", memberName));
            }
            else if (bsa.SerializationType.IsGenericType && bsa.SerializationType.GetGenericTypeDefinition().Equals(typeof(IEnumerable<>)))
            {
                Type elementType = bsa.SerializationType.GetGenericArguments()[0];
                memberType = bsa.SerializationType;
                sbReader.Append(string.Format("\t\tint {0}_count = br.ReadInt32();\r\n", memberName));
                sbReader.Append(string.Format("\t\t{0}[] array = new {0}[{1}_count];\r\n", elementType.FullName, memberName));
                sbReader.Append(string.Format("\t\tfor(int i = 0; i < {0}_count; i++)\r\n", memberName));
                sbReader.Append("\t\t{\r\n");
                sbReader.Append(String.Format("\t\t\tarray[i] = ({0})this.DeserializeType(br);\r\n", elementType.FullName, memberName));
                sbReader.Append("\t\t}\r\n");
                sbReader.Append(string.Format("\t\ttyped.{0} = array;\r\n", memberName));
            }
            else if (bsa.SerializationType.Equals(typeof(IList<>)))
            {
                Type elementType = memberType.GetGenericArguments()[0];
                memberType = bsa.SerializationType;
                sbReader.Append(string.Format("\t\tint {0}_count = br.ReadInt32();\r\n", memberName));
                sbReader.Append(string.Format("\t\tfor(int i = 0; i < {0}_count; i++)\r\n", memberName));
                sbReader.Append("\t\t{\r\n");
                sbReader.Append(String.Format("\t\t\ttyped.{1}.Add(({0})this.DeserializeType(br));\r\n", elementType.FullName, memberName));
                sbReader.Append("\t\t}\r\n");
            }
            else if (bsa.SerializationType.IsGenericType && bsa.SerializationType.GetGenericTypeDefinition().Equals(typeof(IList<>)))
            {
                Type elementType = bsa.SerializationType.GetGenericArguments()[0];
                memberType = bsa.SerializationType;
                sbReader.Append(string.Format("\t\tint {0}_count = br.ReadInt32();\r\n", memberName));
                sbReader.Append(string.Format("\t\tfor(int i = 0; i < {0}_count; i++)\r\n", memberName));
                sbReader.Append("\t\t{\r\n");
                sbReader.Append(String.Format("\t\t\ttyped.{1}.Add(({0})this.DeserializeType(br));\r\n", elementType.FullName, memberName));
                sbReader.Append("\t\t}\r\n");
            }
            else if (memberType.Equals(typeof(DateTime)))
            {
                sbReader.Append(string.Format("\t\ttyped.{0} = DateTime.FromBinary(br.ReadInt64());\r\n", memberName));
            }
            else if (memberType.IsEnum)
            {
                sbReader.Append(string.Format("\t\ttyped.{0} = ({1})(br.Read{2}());\r\n", memberName, memberType.FullName, memberType.GetEnumUnderlyingType().Name));
            }
            else if (PrimitiveSerializer.IsPrimitive(memberType) || memberType.Equals(typeof(String)))
            {
                sbReader.Append(string.Format("\t\ttyped.{0} = br.Read{1}();\r\n", memberName, memberType.Name));
            }
            else
            {
                sbReader.Append(string.Format("\t\t\ttyped.{0} = ({1})this.DeserializeType(br);\r\n", memberName, memberType.FullName));
            }
        }

        private void AddLastBlockWriter(StringBuilder sbWriter)
        {
            sbWriter.Append("\t\tif (typed.AdditionalPayload.Length > 0){\r\n");
            sbWriter.Append("\t\t\tbr.Write(typed.AdditionalPayload);}\r\n");
        }

        private void AddWriter(StringBuilder sbWriter, string memberName, Type memberType, BinarySerializableAttribute bsa)
        {
            if (bsa.SerializationType != null
                && !bsa.SerializationType.Equals(typeof(IList<>)))
            {
                memberType = bsa.SerializationType;
            }

            if (memberType.Equals(typeof(byte[])))
            {
                sbWriter.Append(string.Format("\t\tint {0}_data_length = typed.{0} == null ? 0 : typed.{0}.Length;\r\n", memberName));
                sbWriter.Append(string.Format("\t\tbr.Write({0}_data_length);\r\n", memberName));
                sbWriter.Append(string.Format("\t\tbr.Write(typed.{0} == null ? new byte[0] : typed.{0});\r\n", memberName));
            }
            else if (memberType.IsArray)
            {
                Type arrayType = memberType.GetElementType();
                if (arrayType.IsValueType
                    || arrayType.Equals(typeof(string)))
                {
                    sbWriter.Append(string.Format("\t\tbr.Write(typed.{0}.Length);\r\n", memberName));
                    sbWriter.Append(string.Format("\t\tint {0}_count = typed.{0}.Length;\r\n", memberName));
                    sbWriter.Append(string.Format("\t\tfor(int i = 0; i < {0}_count; i++)\r\n", memberName));
                    sbWriter.Append("\t\t{\r\n");
                    sbWriter.Append(String.Format("\t\t\tbr.Write(({1})typed.{0}[i]);\r\n", memberName, arrayType.FullName));
                    sbWriter.Append("\t\t}\r\n");
                }
                else
                {
                    sbWriter.Append(string.Format("\t\tbr.Write(typed.{0}.Length);\r\n", memberName));
                    sbWriter.Append(string.Format("\t\tint {0}_count = typed.{0}.Length;\r\n", memberName));
                    sbWriter.Append(string.Format("\t\tfor(int i = 0; i < {0}_count; i++)\r\n", memberName));
                    sbWriter.Append("\t\t{\r\n");
                    sbWriter.Append(string.Format("\t\tthis.SerializeType(typed.{0}, br);", memberName));
                    sbWriter.Append("\t\t}\r\n");
                }
            }
            else if (bsa.SerializationType.Equals(typeof(IEnumerable<>)))
            {
                Type elementType = memberType.GetGenericArguments()[0];
                sbWriter.Append(string.Format("\t\tbr.Write(typed.{0}.Count());\r\n", memberName));
                sbWriter.Append(string.Format("\t\tforeach(object item in typed.{0})\r\n", memberName));
                sbWriter.Append("\t\t{\r\n");
                sbWriter.Append(string.Format("\t\tthis.SerializeType(item, br);", memberName));
                sbWriter.Append("\t\t}\r\n");
            }
            else if (bsa.SerializationType.IsGenericType && bsa.SerializationType.GetGenericTypeDefinition().Equals(typeof(IEnumerable<>)))
            {
                Type elementType = bsa.SerializationType.GetGenericArguments()[0];
                sbWriter.Append(string.Format("\t\tbr.Write(typed.{0}.Count());\r\n", memberName));
                sbWriter.Append(string.Format("\t\tforeach(object item in typed.{0})\r\n", memberName));
                sbWriter.Append("\t\t{\r\n");
                sbWriter.Append(string.Format("\t\tthis.SerializeType(item, br);", memberName));
                sbWriter.Append("\t\t}\r\n");
            }
            else if (bsa.SerializationType.Equals(typeof(IList<>)))
            {
                Type elementType = memberType.GetGenericArguments()[0];
                sbWriter.Append(string.Format("\t\tbr.Write(typed.{0}.Count);\r\n", memberName));
                sbWriter.Append(string.Format("\t\tint {0}_count = typed.{0}.Count;\r\n", memberName));
                sbWriter.Append(string.Format("\t\tfor(int i = 0; i < {0}_count; i++)\r\n", memberName));
                sbWriter.Append("\t\t{\r\n");
                sbWriter.Append(string.Format("\t\tthis.SerializeType(typed.{0}[i], br);", memberName));
                sbWriter.Append("\t\t}\r\n");
            }
            else if (bsa.SerializationType.IsGenericType && bsa.SerializationType.GetGenericTypeDefinition().Equals(typeof(IList<>)))
            {
                Type elementType = bsa.SerializationType.GetGenericArguments()[0];
                sbWriter.Append(string.Format("\t\tbr.Write(typed.{0}.Count);\r\n", memberName));
                sbWriter.Append(string.Format("\t\tint {0}_count = typed.{0}.Count;\r\n", memberName));
                sbWriter.Append(string.Format("\t\tfor(int i = 0; i < {0}_count; i++)\r\n", memberName));
                sbWriter.Append("\t\t{\r\n");
                sbWriter.Append(string.Format("\t\tthis.SerializeType(typed.{0}[i], br);", memberName));
                sbWriter.Append("\t\t}\r\n");
            }
            else if (memberType.Equals(typeof(DateTime)))
            {
                sbWriter.Append(string.Format("\t\tbr.Write(typed.{0}.ToBinary());\r\n", memberName));
            }
            else if (memberType.IsEnum)
            {
                sbWriter.Append(string.Format("\t\tbr.Write(({1})typed.{0});\r\n", memberName, memberType.GetEnumUnderlyingType().FullName));
            }
            else if (memberType.Equals(typeof(string)))
            {
                sbWriter.Append(string.Format("\t\tbr.Write(typed.{0} == null ? \"\" : typed.{0});\r\n", memberName));
            }
            else if (PrimitiveSerializer.IsPrimitive(memberType))
            {
                sbWriter.Append(string.Format("\t\tbr.Write(typed.{0});\r\n", memberName));
            }
            else
            {
                sbWriter.Append(string.Format("\t\tthis.SerializeType(typed.{0}, br);", memberName));
            }
        }


        private string GetTemplate()
        {
            return @"using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using Altus.Suffusion.Serialization;
using Altus.Suffusion.IO;
using Altus.Suffusion.Serialization.Binary;

<Using>

namespace <Namespace>
{
    [System.CodeDom.Compiler.GeneratedCodeAttribute(""CSGO"", ""1.0"")]
    [System.Serializable]
    public class BinarySerializer_<TypeName> : <QualifiedTypeName>, ISerializer<<QualifiedTypeName>>
    {       
        protected byte[] OnSerialize(object source)
        {
            <TypeName> typed = (<TypeName>)source;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter br = new BinaryWriter(ms);
<WriteValues>
                return ms.ToArray();
            }
        }

        protected object OnDeserialize(byte[] source, Type targetType)
        {
            using (MemoryStream ms = new MemoryStream(source))
            {
                BinaryReader br = new BinaryReader(ms);
                BinarySerializer_<TypeName> typed = new BinarySerializer_<TypeName>();
<ReadValues>
                return typed;
            }
        }

        protected bool OnSupportsFormats(string format)
        {
            return format.Equals(StandardFormats.BINARY, StringComparison.InvariantCultureIgnoreCase);
        }

        protected virtual void OnDispose()
        {
        }


        IEnumerable<Type> _types = null;
        public IEnumerable<Type> SupportedTypes
        {
            get { return _types; }
        }

        public void Initialize(string name, params string[] args)
        {
            this._types = OnGetSupportedTypes();
            this.Name = name;
        }

        public bool IsInitialized
        {
            get;
            private set;
        }

        public bool IsEnabled
        {
            get;
            set;
        }

        public int Priority
        {
            get;
            private set;
        }

        public bool IsScalar { get { return false; } }

        public bool SupportsFormat(string format)
        {
            return OnSupportsFormats(format);
        }

        public bool SupportsType(Type type)
        {
            return _types != null 
                    && (_types.Contains(type) || type.IsAssignableFrom(typeof(ISerializer<<QualifiedTypeName>>)));
        }

        public byte[] Serialize(object source)
        {
            return OnSerialize(source);
        }

        public object Deserialize(byte[] source, Type targetType)
        {
            return OnDeserialize(source, targetType);
        }

        public event EventHandler Disposed;

        public System.ComponentModel.ISite Site
        {
            get;
            set;
        }

        <NameProperty>

        public void Dispose()
        {
            this.OnDispose();
            if (Disposed != null)
                Disposed(this, new EventArgs());
        }

        public byte[] Serialize(<QualifiedTypeName> source)
        {
            return this.OnSerialize(source);
        }

        public void Serialize(<QualifiedTypeName> source, System.IO.Stream outputStream)
        {
            StreamHelper.Copy(Serialize(source), outputStream);
        }

        public <QualifiedTypeName> Deserialize(byte[] source)
        {
            return (<QualifiedTypeName>)this.OnDeserialize(source, typeof(<QualifiedTypeName>));
        }

        public <QualifiedTypeName> Deserialize(System.IO.Stream inputSource)
        {
            return Deserialize(StreamHelper.GetBytes(inputSource));
        }

        protected IEnumerable<Type> OnGetSupportedTypes()
        {
            return new Type[] { typeof(<QualifiedTypeName>) };
        }

        protected object DeserializeType(BinaryReader br)
        {
            return BinarySerializerBuilder._BinarySerializer.Deserialize(br);
        }
        
        protected void SerializeType(object source, BinaryWriter br)
        {
            BinarySerializerBuilder._BinarySerializer.Serialize(source, br);
        }
    }
}";
        }

        public static class _BinarySerializer
        {
            static Dictionary<Type, ISerializer> _serializers = new Dictionary<Type, ISerializer>();

            public static void Serialize(object source, BinaryWriter bw)
            {
                if (source == null)
                {
                    bw.Write(SerializationContext.Instance.TextEncoding.GetBytes("<null>"));
                }
                else
                {
                    Type t = source.GetType();
                    string tname = t.AssemblyQualifiedName;
                    if (typeof(ISerializer).IsAssignableFrom(t))
                    {
                        Type baseType = t.BaseType;
                        Type serializerGen = typeof(ISerializer<>);
                        Type serializerSpec = serializerGen.MakeGenericType(baseType);
                        if (serializerSpec.IsAssignableFrom(t))
                        {
                            tname = baseType.AssemblyQualifiedName;
                        }
                    }

                    ISerializer serializer = null;
                    lock (_serializers)
                    {
                        try
                        {
                            serializer = _serializers[t];
                        }
                        catch
                        {
                            serializer = App.Resolve<ISerializationContext>().GetSerializer(t, StandardFormats.BINARY);
                            try
                            {
                                _serializers.Add(t, serializer);
                            }
                            catch { }
                        }
                    }
                    if (serializer == null) throw (new System.Runtime.Serialization.SerializationException("Serializer not found for type \"" + tname + "\" supporting the " + StandardFormats.BINARY + " format."));
                    if (t.IsArray)
                    {
                        bw.Write(((Array)source).Length);
                        foreach (object item in (Array)source)
                        {
                            byte[] data = serializer.Serialize(source);
                            bw.Write(tname);
                            bw.Write(data.Length);
                            bw.Write(data);
                        }
                    }
                    else
                    {
                        byte[] data = serializer.Serialize(source);
                        bw.Write(tname);
                        bw.Write(data.Length);
                        bw.Write(data);
                    }
                }
            }

            public static object Deserialize(BinaryReader br)
            {
                string tname = br.ReadString();
                if (tname.Equals("<null>", StringComparison.InvariantCultureIgnoreCase))
                {
                    return null;
                }
                else
                {
                    Type t = TypeHelper.GetType(tname);
                    if (t == null)
                        throw (new System.Runtime.Serialization.SerializationException("Type not found: " + tname));
                    ISerializer serializer = null;
                    lock (_serializers)
                    {
                        try
                        {
                            serializer = _serializers[t];
                        }
                        catch
                        {
                            serializer = App.Resolve<ISerializationContext>().GetSerializer(t, StandardFormats.BINARY);
                            try
                            {
                                _serializers.Add(t, serializer);
                            }
                            catch { }
                        }
                    }
                    if (serializer == null) throw (new System.Runtime.Serialization.SerializationException("Serializer not found for type \"" + tname + "\" supporting the " + StandardFormats.BINARY + " format."));

                    if (t.IsArray)
                    {
                        int count = br.ReadInt32();
                        Array list = (Array)Activator.CreateInstance(t, count);

                        for (int i = 0; i < count; i++)
                        {
                            list.SetValue(serializer.Deserialize(br.ReadBytes(br.ReadInt32()), t), i);
                        }

                        return list;
                    }
                    else
                    {
                        return serializer.Deserialize(br.ReadBytes(br.ReadInt32()), t);
                    }
                }
            }
        }
    }
}
