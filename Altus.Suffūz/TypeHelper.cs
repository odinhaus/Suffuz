using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Altus.Suffūz
{
    public static class TypeHelper
    {
        #region Fields
        #region Static Fields
        static Dictionary<string, Type> _resolvedTypes = new Dictionary<string, Type>();
        #endregion Static Fields

        #region Instance Fields
        #endregion Instance Fields
        #endregion Fields

        #region Event Declarations
        #endregion Event Declarations

        #region Constructors
        #region Public
        #endregion Public

        #region Private
        #endregion Private

        #region Protected
        #endregion Protected
        #endregion  Constructors

        #region Properties
        #region Public
        #endregion Public

        #region Private
        #endregion Private

        #region Protected
        #endregion Protected
        #endregion Properties

        #region Methods
        #region Public
        public static Type GetType(string typeName, bool bThrowIfNotFound)
        {
            Type t = GetType(typeName);
            if (t == null)
                throw new ApplicationException(String.Format("Unable to resolve type '{0}'.", typeName));
            return t;
        }

        //========================================================================================================//
        /// <summary>
        /// Parses the provided string and attempts to resolve a Type for the string
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns>the configured type if it can be found/created, otherwise returns null</returns>
        public static Type GetType(string typeName)
        {
            string[] parts = typeName.Split(',');
            Type retType = null;
            try
            {

                lock (_resolvedTypes)
                {
                    if (_resolvedTypes.ContainsKey(typeName))
                        return _resolvedTypes[typeName];

                    switch (parts.Length)
                    {
                        case 5:
                            {
                                Assembly assem = Assembly.Load(string.Format("{0}, {1}, {2}, {3}", parts[1].Trim(), parts[2].Trim(), parts[3].Trim(), parts[4].Trim()));
                                retType = assem.GetType(parts[0].Trim(), false, true);
                                break;
                            }
                        case 4:
                            {
                                Assembly assem = Assembly.Load(string.Format("{0}, {1}, {2}", parts[1].Trim(), parts[2].Trim(), parts[3].Trim()));
                                retType = assem.GetType(parts[0].Trim(), false, true);
                                break;
                            }
                        case 3:
                            {
                                Assembly assem = Assembly.Load(string.Format("{0}, {1}", parts[1].Trim(), parts[2].Trim()));
                                retType = assem.GetType(parts[0].Trim(), false, true);
                                break;
                            }
                        case 2: // the assemblyname, typename was supplied
                            {
                                Assembly assem = Assembly.Load(parts[1].Trim());
                                retType = assem.GetType(parts[0].Trim(), false, true);
                                break;
                            }
                        default:
                            {
                                retType = Type.GetType(typeName, false, true);
                                break;
                            }
                    }

                    _resolvedTypes.Add(typeName, retType);
                }
                return retType;
            }
            catch
            {
                if (parts.Length >= 2)
                {
                    var asm = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name.Equals(parts[1].Trim(), StringComparison.CurrentCultureIgnoreCase));
                    if (asm != null)
                    {
                        retType = asm.GetTypes().SingleOrDefault(t => t.FullName.Equals(parts[0].Trim()));
                        _resolvedTypes.Add(typeName, retType);
                        return retType;
                    }
                }
                return null;
            }
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Gets the type in the provided assembly by name
        /// </summary>
        /// <param name="typeName"></param>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public static Type GetType(string typeName, string assemblyName)
        {
            try
            {
                Assembly assem;
                if (File.Exists(assemblyName))
                    assem = Assembly.LoadFrom(assemblyName);
                else
                    assem = Assembly.Load(assemblyName.Trim());
                return assem.GetType(typeName.Trim());
            }
            catch
            {
                return null;
            }
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Creates an instance of the type specified by typeName
        /// </summary>
        /// <param name="typeName"></param>
        /// <param name="activationArgs"></param>
        /// <returns>new instance of type, otherwise null</returns>
        public static object CreateType(string typeName, object[] activationArgs)
        {
            Type type = GetType(typeName);
            if (type != null)
            {
                return Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    activationArgs,
                    Thread.CurrentThread.CurrentCulture);
            }
            else
            {
                return null;
            }
        }
        //========================================================================================================//


        /// <summary>
        /// Returns true if the current type implements the provided concrete interface type, or generic interface type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool Implements<T>(this Type type)
        {
            return Implements(type, typeof(T));
        }

        /// <summary>
        /// Returns true if the current type implements the provided concrete interface type, or generic interface type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="interfaceType"></param>
        /// <returns></returns>
        public static bool Implements(this Type type, Type interfaceType)
        {
            return type == interfaceType
                || (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == interfaceType)
                || type.GetTypeInfo().ImplementedInterfaces.Any(i =>
                    i.Equals(interfaceType) || (i.IsConstructedGenericType && i.GetGenericTypeDefinition().Equals(interfaceType)));
        }

        public static bool IsTypeOrSubtypeOf<T>(this Type type)
        {
            return type == typeof(T) || type.IsSubclassOf(typeof(T));
        }

        #endregion Public

        #region Private
        #endregion Private

        #region Protected
        #endregion Protected
        #endregion Methods

        #region Event Handlers and Callbacks
        #region Public
        #endregion Public

        #region Private
        #endregion Private

        #region Protected
        #endregion Protected
        #endregion Event Handlers and Callbacks
    }
}
