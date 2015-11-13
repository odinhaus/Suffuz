using Altus.Suffusion;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Diagnostics
{
    public enum PerformanceCounterCategories
    {
        Core,
        Data,
        Comm
    }

    /// <summary>
    /// The of CSGO instance types
    /// </summary>
    public enum CounterInstanceType
    {
        /// <summary>
        /// perf counter is created only once - during install - no dynamic instances allowed/ 
        /// </summary>
        Static,
        /// <summary>
        /// perf counter is NOT created during install - only during runtime
        /// </summary>
        Dynamic
    }

    [Serializable()]
    public class PerformanceCounter : IDisposable
    {
        /// <summary>
        /// The suffix for the base type name 
        /// </summary>
        public static String BaseSuffix = " base";

        #region Fields

        /// <summary>
        /// This counters category
        /// </summary>
        private string _category = String.Empty;

        /// <summary>
        /// This counters name
        /// </summary>
        private string _name = String.Empty;

        /// <summary>
        /// The counters help string
        /// </summary>
        private string _counterHelp = String.Empty;

        /// <summary>
        /// The specific calculation formula for our counter
        /// </summary>
        private System.Diagnostics.PerformanceCounterType _type;

        /// <summary>
        /// The actual NT Performance counter
        /// </summary>             
        private System.Diagnostics.PerformanceCounter _perfCount = null;

        /// <summary>
        /// The actual NT Performance counter base element
        /// </summary>
        private System.Diagnostics.PerformanceCounter _perfBase = null;

        /// <summary>
        /// The instance - some unique moniker to specify an instance - IP address or path
        /// </summary>
        private string _instance = null;

        /// <summary>
        /// current incremental value of the counter
        /// </summary>
        private int _count = 0;

        /// <summary>
        /// Ensures that intervals are scaled correctly
        /// </summary>        
        private long _ticks = 0;

        /// <summary>
        /// Records if GC has been attempted
        /// </summary>
        protected bool disposed = false;

        /// <summary>
        /// relative count?
        /// </summary>
        protected int _relativeCount = 0;

        /// <summary>
        /// Counter Instance Type
        /// </summary>
        protected CounterInstanceType _counterInstanceType = CounterInstanceType.Static;

        #region Static to all instances (Global)
        /// <summary>
        /// The instance - some unique moniker to specify an instance - IP address or path
        /// </summary>
        private static string _AppInstance = String.Empty;

        /// <summary>
        /// A dictionary of counter data to help in deriving categories keyed by categories
        /// </summary>
        private static Dictionary<string, CounterCreationDataCollection> _catCreationData = new Dictionary<string, CounterCreationDataCollection>();

        /// <summary>
        /// list of categories keyd by CounterCreationData
        /// </summary>
        private static Dictionary<CounterCreationData, string> _ccdCategoryDict = new Dictionary<CounterCreationData, string>();

        /// <summary>
        /// Our homegrown perfmon installation attributes
        /// </summary>
        private static PerformanceCounterInstallAttribute[] _perfmonInstallAttributes;

        /// <summary>
        /// dictionary of perfcounter installation attributes
        /// </summary>
        private static Dictionary<string, PerformanceCounterInstallAttribute> _perfCounterInstallAttrDict = new Dictionary<string, PerformanceCounterInstallAttribute>();

        /// <summary>
        /// Performance counters for this instance
        /// </summary>
        private static Dictionary<String, PerformanceCounter> _perfCounters = new Dictionary<string, PerformanceCounter>();

        /// <summary>
        /// A list of performance counter category enums
        /// </summary>
        private static List<PerformanceCounterCategories> _pccList = new List<PerformanceCounterCategories>((PerformanceCounterCategories[])Enum.GetValues(typeof(PerformanceCounterCategories)));
        #endregion

        #endregion

        #region Properties
        //========================================================================================================//
        /// <summary>
        /// Returns the calculated counter value relative to the counter's type
        /// </summary>
        public float Value
        {
            get
            {
                if (_perfCount == null)
                    return 0f;

                return _perfCount.NextValue();
            }
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// Gets the current relative count since this particular counter instance
        /// was instantiated.  This lifetime is bounded by the PerformanceCounter
        /// instance (.Net) lifetime, not the actual performance counter's
        /// lifetime at the OS level.  This property is best used when
        /// you want to limit performance monitoring based on a certain
        /// number of operations after the instantiation on the PerformanceCounter
        /// instance.
        /// </summary>
        public long RelativeCount
        {
            get { return _relativeCount; }
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// Gets the current incremental value of the counter.  
        /// This count is absolute for the lifetime of the counter (or counter instance).
        /// </summary>
        public long Count
        {
            get { return _count; }
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// The counter's instance name (if applicaple)
        /// </summary>
        public string Instance
        {
            get { return _instance; }
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// The type of performance counter represented by this instance
        /// </summary>
        public System.Diagnostics.PerformanceCounterType Type
        {
            get { return _type; }
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// The performance counter name represented by this instance
        /// </summary>
        public string Name
        {
            get { return _name; }
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// The performance counter help string represented by this instance
        /// </summary>
        public string CounterHelp
        {
            get { return _counterHelp; }
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// The performance counter category that contains this counter instance
        /// </summary>
        public string Category
        {
            get { return _category; }
        }
        //========================================================================================================//

        #endregion

        #region Methods

        #region counter access and instance creation

        /// <summary>
        /// Provide a global easy way to access counters that are created via assembly attributes
        /// and CreateInstance()
        /// </summary>
        /// <param name="counterName">The counter name</param>
        /// <returns>The counter</returns>
        public static PerformanceCounter GetPerfCounterInstance(string name)
        {
            String category = String.Empty;
            if (_perfCounters.ContainsKey(name) == true)
                return _perfCounters[name];
            else
                return new PerformanceCounter("Undefined", name, "Undefined", (PerformanceCounterType)(-1));
        }

        public static PerformanceCounter GetPerfCounterInstance(string name, string newInstanceName)
        {
            String category = String.Empty;
            if (_perfCounters.ContainsKey(name) == true)
            {
                PerformanceCounter pcDefault = _perfCounters[name];
                return new PerformanceCounter(pcDefault.Category, pcDefault.Name, pcDefault.CounterHelp, pcDefault.Type, newInstanceName);
            }
            else
                return new PerformanceCounter("Undefined", name, "Undefined", (PerformanceCounterType)(-1));
        }

        #endregion

        #region Constructors

        #region Indexer for perfcounter

        /// <summary>
        /// Return an instance of a counter via this indexer
        /// </summary>
        /// <param name="instanceName"></param>
        /// <returns>An instance of a created</returns>
        public PerformanceCounter this[String instanceName]
        {

            set
            {
                if ((int)this.Type == -1) return;
                lock (this)
                {
                    try
                    {
                        if (this._perfCount == null)
                        {
                            this._perfCount = new System.Diagnostics.PerformanceCounter(this._category, this._name,
                                String.Format("{0}@{1}", instanceName, _AppInstance), false);

                            PerformanceCounterType perfCounterTypeBase;
                            bool needsABaseType = NeedsABaseType(this._type, out perfCounterTypeBase);

                            if (needsABaseType == true)
                            {
                                this._perfBase =
                                    new System.Diagnostics.PerformanceCounter(this._category, this._name + BaseSuffix,
                                        String.Format("{0}@{1}", instanceName, _AppInstance), false);
                            }
                        }
                        else
                        {
                            this._perfCount.InstanceName = String.Format("{0}@{1}", instanceName, _AppInstance);
                            if (this._perfBase != null)
                                this._perfBase.InstanceName = String.Format("{0}@{1}", instanceName, _AppInstance);
                        }
                    }
                    catch
                    {
                        return;
                    }

                }
            }

            get
            {
                if ((int)this.Type == -1) return this;
                lock (this)
                {
                    try
                    {
                        if (this._perfCount == null)
                        {
                            this._perfCount =
                                new System.Diagnostics.PerformanceCounter(this._category, this._name,
                                    String.Format("{0}@{1}", instanceName, _AppInstance), false);

                            PerformanceCounterType perfCounterTypeBase;
                            bool needsABaseType = NeedsABaseType(this._type, out perfCounterTypeBase);

                            if (needsABaseType == true)
                            {
                                this._perfBase =
                                    new System.Diagnostics.PerformanceCounter(this._category, this._name + BaseSuffix,
                                        String.Format("{0}@{1}", instanceName, _AppInstance), false);
                            }
                        }
                        else
                        {
                            this._perfCount.InstanceName = String.Format("{0}@{1}", instanceName, _AppInstance);
                            if (this._perfBase != null)
                                this._perfBase.InstanceName = String.Format("{0}@{1}", instanceName, _AppInstance);
                        }
                    }
                    catch { }
                    return this;
                }
            }
        }
        #endregion


        /// <summary>
        /// Grab the CSGO instance name - either the friendly name or product key
        /// </summary>
        static PerformanceCounter()
        {
#if BREAKME
            System.Diagnostics.Debugger.Break();
#endif
            try
            {
                _AppInstance = App.InstanceName;
            }
            catch
            {
                _AppInstance = "UnknownNode:";
            }

            LoadPerfmonInstallerAttributes();
            InstallPerfmonCounterCategories();
            InitializePerfmonCounters();
        }


        /// <summary>
        /// manage our performance monitors deserialization behavior
        /// </summary>
        /// <param name="info">Stores all the data to de/serialize this class</param>
        /// <param name="context">Describes source and destination of this serialized object</param>
        protected PerformanceCounter(SerializationInfo info, StreamingContext context)
        {
            _ticks = info.GetInt64("_ticks");
            _relativeCount = info.GetInt32("RelativeCount");
            _count = info.GetInt32("Count");
            _instance = info.GetString("Instance");
            _type = (PerformanceCounterType)info.GetValue("Type", typeof(System.Diagnostics.PerformanceCounterType));
            _name = info.GetString("Name");
            _counterHelp = info.GetString("CounterHelp");
            _category = info.GetString("Category");

        }

        /// <summary>
        /// manage our performance monitors serialization behavior
        /// </summary>
        /// <param name="info">Stores all the data to de/serialize this class</param>
        /// <param name="context">Describes source and destination of this serialized object</param>
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("_ticks", _ticks);
            info.AddValue("RelativeCount", _relativeCount);
            info.AddValue("Count", _count);
            info.AddValue("Instance", _instance);
            info.AddValue("Type", Type, typeof(System.Diagnostics.PerformanceCounterType));
            info.AddValue("Name", _name);
            info.AddValue("CounterHelp", _counterHelp);
            info.AddValue("Category", _category);
        }

        /// <summary>
        /// Construct a helper class to manage Perfmon counters for Connect
        /// </summary>
        /// <param name="category">The category for this counter</param>
        /// <param name="name">This counters name</param>
        /// <param name="counterHelp">This counters help string</param>
        /// <param name="type">This counters type</param>
        /// <param name="instance"></param>
        public PerformanceCounter(string category, string name, string counterHelp,
            PerformanceCounterType type, String instance)
        {
            _category = category;
            _name = name;
            _counterHelp = counterHelp;
            _type = type;
            _counterInstanceType = String.IsNullOrEmpty(instance) == false ? CounterInstanceType.Static : CounterInstanceType.Dynamic;
            _instance = instance;
        }

        /// <summary>
        /// Construct a helper class to manage Perfmon counters for Altus.  This constructor
        /// builds an intance that is dynamic within this web instance.  For example, it is used
        /// for counters on the Zone Category
        /// </summary>
        /// <param name="category">The category for this counter</param>
        /// <param name="name">This counters name</param>
        /// <param name="counterHelp">This counters help string</param>
        /// <param name="type">This counters type</param>
        public PerformanceCounter(string category, string name, string counterHelp, PerformanceCounterType type)
            : this(category, name, counterHelp, type, null)
        {
        }

        #endregion

        #region Creation and mangement of counter instances
        /// <summary>
        /// Create the actual instance
        /// </summary>
        /// <param name="pc">Program Counter Instance to create</param>
        /// <returns>The created program counter</returns
        public static PerformanceCounter CreateCounterInstance(PerformanceCounter pc)
        {
            if (pc == null || String.IsNullOrEmpty(pc.Name) == true) return null;
            try
            {
                pc._perfCount = new System.Diagnostics.PerformanceCounter(pc.Category, pc.Name, pc.Instance, pc.ReadOnly);

                PerformanceCounterType perfCounterTypeBase;
                bool needsABaseType = NeedsABaseType(pc.Type, out perfCounterTypeBase);

                if (needsABaseType == true)
                {
                    pc._perfBase = new System.Diagnostics.PerformanceCounter(pc.Category, pc.Name + BaseSuffix, pc.Instance, false);
                }
            }
            catch (InvalidOperationException ioe)
            {
                Logger.LogError("Could not create performance counter: " + pc.Name + " " + ioe.Message);
            }
            return pc;
        }

        public static PerformanceCounter CreateCounterInstance(string category, string name, string instance, PerformanceCounterType type, bool readOnly)
        {
            return CreateCounterInstance(new PerformanceCounter(category, name, "", type, instance) { ReadOnly = readOnly });
        }

        //========================================================================================================//
        /// <summary>
        /// Creates friendly counter categories from enums
        /// </summary>
        /// <param name="cat"></param>
        /// <returns></returns>
        public static string CounterCategory(PerformanceCounterCategories cat)
        {
            String perfCounterSource = App.InstanceName;

            switch (cat)
            {
                case PerformanceCounterCategories.Comm:
                    return "CSGO:Communications:" + perfCounterSource;
                case PerformanceCounterCategories.Core:
                    return "CSGO:Core:" + perfCounterSource;
                case PerformanceCounterCategories.Data:
                    return "CSGO:Data:" + perfCounterSource;
                default:
                    Logger.Log("Category Name not found: " + cat.ToString());
                    return cat.ToString().Replace("_", ":") + ":" + perfCounterSource; ;
            }
        }

        //========================================================================================================//
        /// <summary>
        /// Creates help string from category name
        /// </summary>
        /// <param name="cat">The category name</param>
        /// <returns>The help string</returns>
        public static String CategoryHelp(String cat)
        {
            String helpString = String.Empty;

            try
            {
                string[] words = cat.Split(new char[] { ':' });
                string enumString = String.Format("{0}_{1}", words[0], words[1]);
                PerformanceCounterCategories pcc =
                    (PerformanceCounterCategories)Enum.Parse(typeof(PerformanceCounterCategories), enumString);
                switch (pcc)
                {
                    case PerformanceCounterCategories.Comm:
                        helpString = "CSGO Device and Desktop Communications Counters";
                        break;
                    case PerformanceCounterCategories.Core:
                        helpString = "CSGO Core Functionality Counters";
                        break;
                    case PerformanceCounterCategories.Data:
                        helpString = "CSGO Data Access Counters";
                        break;
                }
            }
            catch
            {
                helpString = "CSGO General Counters";
            }
            return helpString;
        }

        /// <summary>
        /// Load an array of permon installer attributes
        /// </summary>
        /// <returns></returns>
        public static PerformanceCounterInstallAttribute[] LoadPerfmonInstallerAttributes()
        {
            String asmDir = String.Empty;
            object[] assemblies = new object[0];
            List<PerformanceCounterInstallAttribute> _pcias = new List<PerformanceCounterInstallAttribute>();

            object domainAssemblies = AppDomain.CurrentDomain.GetData("Assemblies");
            if (domainAssemblies == null
                || (domainAssemblies is List<Assembly> && ((List<Assembly>)domainAssemblies).Count == 0))
            {
                // launched outside launcher, so try a local lookup
                domainAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                assemblies = (object[])domainAssemblies;
            }
            else
            {
                // launcher will set a list of assemblies on the domain
                // get them here and convert to an array
                assemblies = (object[])((List<Assembly>)domainAssemblies).ToArray();
            }

            // could be a filename array or an assembly array
            for (int i = 0; i < assemblies.Length; i++)
            {
                object asm = assemblies[i];
                try
                {
                    string filename;
                    if (asm is string)
                    {
                        filename = asm.ToString();
                        // If it doesn't exist then bail out
                        if (String.IsNullOrEmpty(filename) == true || File.Exists(filename) == false)
                            continue;

                        _perfmonInstallAttributes = ((PerformanceCounterInstallAttribute[])Assembly.LoadFrom(filename).GetCustomAttributes(typeof(PerformanceCounterInstallAttribute), false));
                    }
                    else
                    {
                        filename = ((Assembly)asm).GetName().Name;
                        _perfmonInstallAttributes = ((PerformanceCounterInstallAttribute[])((Assembly)asm).GetCustomAttributes(typeof(PerformanceCounterInstallAttribute), false));
                    }
                    // add attributes to the dictionary
                    foreach (PerformanceCounterInstallAttribute pcia in _perfmonInstallAttributes)
                    {
                        if (!_perfCounterInstallAttrDict.ContainsKey(pcia.Name))
                            _perfCounterInstallAttrDict.Add(pcia.Name, pcia);
                        else
                            _perfCounterInstallAttrDict[pcia.Name] = pcia;
                    }

                    foreach (PerformanceCounterInstallAttribute attr in _perfmonInstallAttributes)
                    {
                        CounterCreationData counterCreationData = new CounterCreationData();
                        counterCreationData.CounterName = attr.Name;
                        counterCreationData.CounterHelp = attr.CounterHelp;
                        counterCreationData.CounterType = attr.PerformanceCounterType;
                        if (_catCreationData.ContainsKey(CounterCategory(attr.Category)) == false)
                            _catCreationData.Add(CounterCategory(attr.Category), new CounterCreationDataCollection());
                        _catCreationData[CounterCategory(attr.Category)].Add(counterCreationData);  // add the counter to the correct category
                        if (_ccdCategoryDict.ContainsKey(counterCreationData) == false)
                            _ccdCategoryDict.Add(counterCreationData, CounterCategory(attr.Category));
                    }
                    if (_perfmonInstallAttributes.Length > 0)
                    {
                        _pcias.AddRange(_perfmonInstallAttributes);
                    }
                    Logger.Log(String.Format("LoadPerfmonInstallerAttributes() - Successfully loaded assembly {0}",
                        filename));
                }
                catch (ArgumentNullException ane)
                {
                    Logger.Log(String.Format("LoadPerfmonInstallerAttributes() - could not load attributes from {0}",
                        ane.ParamName));
                }
                catch (ArgumentException ae)
                {
                    Logger.Log(String.Format("LoadPerfmonInstallerAttributes() - could not load attribute Param={0}: {1}.",
                        ae.ParamName, ae.Message));

                }
                catch (System.IO.FileNotFoundException fnfe)
                {
                    Logger.Log(String.Format("LoadPerfmonInstallerAttributes() - unable to load assembly {0} because {1}.",
                        fnfe.FileName, fnfe.Message));

                }
                catch (Exception ex)
                {
                    Logger.Log(String.Format("LoadPerfmonInstallerAttributes() - unable to load assembly: {0}.",
                        ex.Message));
                }
            }
            return _pcias.ToArray();
        }

        /// <summary>
        /// Install the perfmon counters
        /// </summary>
        /// <returns>true if successfully loaded else false</returns>
        public static void InstallPerfmonCounterCategories()
        {
            String categoryName = String.Empty;
            Dictionary<string, CounterCreationDataCollection> catCreationData = null;

            // Iterate through each category
            foreach (PerformanceCounterCategories pccEntry in _pccList)
            {
                try
                {
                    catCreationData = new Dictionary<string, CounterCreationDataCollection>();
                    categoryName = CounterCategory(pccEntry);
                    Logger.Log(String.Format("InstallPerfmonCounterCategories() - attempting to create custom perfmon installer for {0}\n",
                       categoryName));
                    CounterCreationDataCollection ccdc = new CounterCreationDataCollection();

                    if (_catCreationData.ContainsKey(categoryName) == false) continue;
                    // Add the counter data to the installer counters collection
                    foreach (CounterCreationData counterCreationData in _catCreationData[categoryName])
                    {
                        if (ccdc.Contains(counterCreationData) == true)
                            continue;
                        ccdc.Add(counterCreationData);

                        PerformanceCounterType perfCounterTypeBase;
                        bool needsABaseType = NeedsABaseType(counterCreationData.CounterType, out perfCounterTypeBase);

                        // Since this counter type needs a base, create it and add it to the collection
                        if (needsABaseType == true)
                        {
                            CounterCreationData counterBaseCreationData = new CounterCreationData();
                            counterBaseCreationData.CounterName = counterCreationData.CounterName + BaseSuffix;
                            counterBaseCreationData.CounterHelp = counterCreationData.CounterHelp + BaseSuffix;
                            counterBaseCreationData.CounterType = perfCounterTypeBase;
                            // Add a base counter to collection of counterCreationData previous was an average type
                            if (ccdc.Contains(counterBaseCreationData) == false)
                                ccdc.Add(counterBaseCreationData);
                        }

                        Logger.Log(String.Format("InstallPerfmonCounterCategories() - added counter {0}, category {1} of type {2}\n",
                            counterCreationData.CounterName, counterCreationData.CounterHelp, counterCreationData.CounterType));
                    }
                    catCreationData.Add(categoryName, ccdc);
                }
                catch (ArgumentNullException ane)
                {
                    Logger.Log(String.Format("InstallPerfmonCounterCategories() - Custom attribute {0} not constructed correctly.\n",
                        ane.ParamName));
                }
                catch (ArgumentException ae)
                {
                    Logger.Log(String.Format("InstallPerfmonCounterCategories() - Not able to add counter {0) to installer.\n",
                        ae.ParamName));
                }
                catch (TypeLoadException tle)
                {
                    Logger.Log(String.Format("InstallPerfmonCounterCategories() - Not able to load custom attributes because {0}.\n",
                        tle.Message));
                }
                catch (Exception ex)
                {
                    Logger.Log(String.Format("InstallPerfmonCounterCategories() - Not able to load custom attributes because {0}.\n",
                        ex.Message));
                }
            }
        }

        /// <summary>
        /// Install the actual perfmon counters for this instance.
        /// </summary>
        public static void InitializePerfmonCounters()
        {
            PerformanceCounter pc = null;

            // Iterate through each category
            foreach (String categoryName in _catCreationData.Keys)
            {
                Logger.Log(String.Format("InstallPerfmonCounters() - attempting to create custom perfmon counters for {0} instance {1}\n",
                   categoryName, _AppInstance));

                // create the actual counter create data collection for the installer
                CounterCreationDataCollection counterCreationDataCollection = _catCreationData[categoryName];

                foreach (CounterCreationData ccd in counterCreationDataCollection)
                {
                    // skip base creation, this is done by the CSGO ctor
                    if (ccd.CounterName.Contains("base") == true) continue;
                    // If dynamically created, don't create now
                    CounterInstanceType ConnectInstanceType = _perfCounterInstallAttrDict[ccd.CounterName].ConnectInstanceType;

                    // Create the perfcounter

                    if (ConnectInstanceType == CounterInstanceType.Static)
                    {
                        pc =
                            CreateCounterInstance(new PerformanceCounter(categoryName, ccd.CounterName, ccd.CounterHelp,
                                ccd.CounterType, _AppInstance));
                    }
                    else  //dynamic type - don't instance the perfcounter
                    {
                        pc = new PerformanceCounter(categoryName, ccd.CounterName, ccd.CounterHelp, ccd.CounterType);
                        pc = PerformanceCounter.CreateCounterInstance(pc);
                    }
                    _perfCounters.Add(ccd.CounterName, pc);
                }
            }
        }


        /// <summary>
        /// Determines if a performance counter needs a base type
        /// </summary>
        /// <param name="counterType">The counter type to check</param>
        /// <param name="perfCounterTypeBase">The basetype to use if needed</param>
        /// <returns>true if it needs a base type</returns>
        public static bool NeedsABaseType(PerformanceCounterType CounterType, out PerformanceCounterType perfCounterTypeBase)
        {
            bool needsABaseType = false;
            perfCounterTypeBase = PerformanceCounterType.AverageBase;
            // determine if the counter requires a base type
            switch (CounterType)
            {
                case PerformanceCounterType.AverageTimer32:
                case PerformanceCounterType.AverageCount64:
                    perfCounterTypeBase = PerformanceCounterType.AverageBase;
                    needsABaseType = true;
                    break;
                case PerformanceCounterType.CounterMultiTimer:
                case PerformanceCounterType.CounterMultiTimerInverse:
                case PerformanceCounterType.CounterMultiTimer100Ns:
                case PerformanceCounterType.CounterMultiTimer100NsInverse:
                    perfCounterTypeBase = PerformanceCounterType.CounterMultiBase;
                    needsABaseType = true;
                    break; ;
                case PerformanceCounterType.RawFraction:
                    perfCounterTypeBase = PerformanceCounterType.RawBase;
                    needsABaseType = true;
                    break;
                case PerformanceCounterType.SampleFraction:
                    perfCounterTypeBase = PerformanceCounterType.SampleBase;
                    needsABaseType = true;
                    break;
                default:
                    needsABaseType = false;
                    break;
            }
            return needsABaseType;
        }
        #endregion

        //========================================================================================================//
        /// <summary>
        /// Deletes a category if it exists and the user has permission.
        /// This method throws no exceptions, even if the delete fails.
        /// </summary>
        /// <param name="category"></param>
        /// <returns>true if successful ,otherwise false</returns>
        public static bool Delete(string category)
        {
            try
            {
                PerformanceCounterCategory.Delete(category);
                return true;
            }
            catch { }
            return false;
        }
        //========================================================================================================//

        public void IncrementBy(int count)
        {
            IncrementBy(count, 1);
        }

        public void IncrementByFast(int count)
        {
            IncrementByFast(count, 1);
        }

        //========================================================================================================//
        /// <summary>
        /// Call this method prior to disposal to increment
        /// or decrement (by using a negative value) the value of the counter.
        /// 
        /// Disposal without calling this method will increment the counter by 1, by default.
        /// 
        /// Repeated calls to this method will tally the sum total of all previous calls to this method
        /// and that value will be submitted to the counter when disposing.
        /// </summary>
        /// <param name="count"></param>
        public void IncrementBy(int count, int perBase)
        {
            if (_perfCount == null) return;

            _count += count;
            _relativeCount += count;

            if (_perfBase == null)
            {
                _perfCount.IncrementBy(count);
                return;
            }

            if (_type == PerformanceCounterType.AverageTimer32)
            {
                double time = MetricsHelper.StopCallTimer(_category + _name + (_instance == null ? "" : _instance));
                _ticks = (long)(time / MetricsHelper.TimerFrequency);
                _perfCount.IncrementBy(_ticks);
                _perfBase.IncrementBy(count);
            }
            else if (_type == PerformanceCounterType.AverageCount64
                || _type == PerformanceCounterType.RawFraction
                || _type == PerformanceCounterType.SampleFraction)
            {
                _perfCount.IncrementBy(count);
                _perfBase.IncrementBy(perBase);
            }
        }
        //========================================================================================================//


        public void IncrementByFast(int count, int perBase)
        {
            if (_perfCount == null) return;

            _count += count;
            _relativeCount += count;

            if (_perfBase == null)
            {
                _perfCount.RawValue += count;
                return;
            }

            if (_type == PerformanceCounterType.AverageTimer32)
            {
                double time = MetricsHelper.StopCallTimer(_category + _name + (_instance == null ? "" : _instance));
                _ticks = (long)(time / MetricsHelper.TimerFrequency);
                _perfCount.RawValue += _ticks;
                _perfBase.RawValue += count;
            }
            else if (_type == PerformanceCounterType.AverageCount64
                || _type == PerformanceCounterType.RawFraction
                || _type == PerformanceCounterType.SampleFraction)
            {
                _perfCount.RawValue += count;
                _perfBase.RawValue += perBase;
            }
        }

        //========================================================================================================//
        /// <summary>
        /// For Average timespan timers, this method should be called prior to increment to set the
        /// timer's start time for use in calculating the average time span
        /// </summary>
        public void SetTime()
        {
            MetricsHelper.StartCallTimer(_category + _name + (_instance == null ? "" : _instance));
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Sets the counter's Count value to an absolute value.
        /// The relative count for this counter's instance is NOT changed during this operation.
        /// </summary>
        /// <param name="count"></param>
        public void SetCount(long count)
        {
            if (_perfCount == null) return;
            _perfCount.RawValue = count;
        }
        //========================================================================================================//

        public void SetCount(long count, long baseCount)
        {
            if (_perfCount == null) return;
            _perfCount.RawValue = count;
            if (_perfBase == null) return;
            _perfBase.RawValue = baseCount;
        }

        //========================================================================================================//
        /// <summary>
        /// Sets the counter's Count value to an absolute value.
        /// The relative count can be optinally synchronized to match the same value, so that the
        /// relative count variance from current to absolute is the same.
        /// </summary>
        /// <param name="count"></param>
        /// <param name="resetRelative"></param>
        public void SetCount(long count, bool resetRelative)
        {
            SetCount(count);
            if (_perfCount == null || resetRelative == false) return;
            _perfCount.IncrementBy(-(count));
            _count -= (int)count;
        }
        //========================================================================================================//
        #endregion

        /// <summary>
        /// Remove the named instance
        /// </summary>
        /// <param name="instance"></param>
        public void RemoveInstance()
        {
            if (_perfCount != null && (PerformanceCounterCategory.Exists(_perfCount.CategoryName) == false ||
                PerformanceCounterCategory.InstanceExists(_perfCount.InstanceName, _perfCount.CategoryName) == false))
                return;

            if (_perfBase != null)
            {
                _perfBase.RemoveInstance();
            }
            if (_perfCount != null)
            {
                _perfCount.RemoveInstance();
            }
        }

        #region Overloads
        //========================================================================================================//
        /// <summary>
        /// Cast from PerformanceCounter to long
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static implicit operator int (PerformanceCounter counter)
        {
            if (counter == null) return 0;

            return counter._count;
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Increments the counter by 1
        /// </summary>
        /// <param name="counter"></param>
        /// <returns></returns>
        public static PerformanceCounter operator ++(PerformanceCounter counter)
        {
            if (counter == null) return null;

            counter.IncrementBy(1); // updates the counter
            return counter;
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Decrements the counter by 1
        /// </summary>
        /// <param name="counter"></param>
        /// <returns></returns>
        public static PerformanceCounter operator --(PerformanceCounter counter)
        {
            if (counter == null) return null;

            counter.IncrementBy(-1);
            return counter;
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Allows the developer to use + to increment the counter value
        /// </summary>
        /// <param name="counter"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static PerformanceCounter operator +(PerformanceCounter counter, int value)
        {
            if (counter == null) return null;

            counter.IncrementBy(value);
            return counter;
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Performs less-than comparison with int value
        /// </summary>
        /// <param name="counter"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool operator <(PerformanceCounter counter, int value)
        {
            if (counter == null) return true;

            return counter._count < value;
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// Performs greater-then comparison with int value
        /// </summary>
        /// <param name="counter"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool operator >(PerformanceCounter counter, int value)
        {
            if (counter == null) return false;

            return counter._count > value;
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Allows the developer to use - to decrement the counter value
        /// </summary>
        /// <param name="counter"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static PerformanceCounter operator -(PerformanceCounter counter, int value)
        {
            if (counter == null) return null;

            counter.IncrementBy(-value);
            return counter;
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Allows the developer to compare the counter to an int value for use in scenarios
        /// like for loops
        /// </summary>
        /// <param name="counter"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool operator ==(PerformanceCounter counter, int value)
        {
            if (counter == null) return false;

            return counter._count == value;
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Allows developer to perform integer comparisons in scenarios like for loops
        /// </summary>
        /// <param name="counter"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool operator !=(PerformanceCounter counter, int value)
        {
            if (counter == null) return true;

            return counter._count != value;
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Performs equality comparison
        /// </summary>
        /// <param name="counter1"></param>
        /// <param name="counter2"></param>
        /// <returns></returns>
        public static bool operator ==(PerformanceCounter counter1, PerformanceCounter counter2)
        {
            if (object.Equals(counter1, null) && object.Equals(counter2, null)) return true;
            if (object.Equals(counter1, null) || object.Equals(counter2, null)) return false;

            return counter1._category == counter2._category && counter1._name == counter2._name
            && counter1._instance == counter2._instance && counter1._type == counter2._type
            && counter1._count == counter2._count;
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Performs inequality comparison
        /// </summary>
        /// <param name="counter1"></param>
        /// <param name="counter2"></param>
        /// <returns></returns>
        public static bool operator !=(PerformanceCounter counter1, PerformanceCounter counter2)
        {
            if (object.Equals(counter1, null) && object.Equals(counter2, null)) return false;
            if (object.Equals(counter1, null) || object.Equals(counter2, null)) return true;

            return !(counter1._category == counter2._category && counter1._name == counter2._name
            && counter1._instance == counter2._instance && counter1._type == counter2._type
            && counter1._count == counter2._count);
        }
        //========================================================================================================//

        #endregion

        #region Overrides
        /// <summary>
        /// Make sure each instance is unique
        /// </summary>
        /// <returns>hashcode for this instance</returns>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ (_perfBase != null ? _perfBase.GetHashCode() : Int32.MinValue.GetHashCode()) ^ _perfCount.GetHashCode() ^
                _instance.GetHashCode() ^ _name.GetHashCode() ^ _type.GetHashCode();
        }

        /// <summary>
        /// Checks to see if two performance counters are equal
        /// </summary>
        /// <param name="obj">A performance counter to be compared with this counter</param>
        /// <returns>true if the performance counters are equal</returns>
        /// <remarks>Both the base counter and performance counter (if defined) must be identical to return a true</remarks>
        public override bool Equals(object obj)
        {
            if (obj == null && _perfCount == null) return true;

            if (_perfCount == null) return false;

            if (obj is PerformanceCounter == false) return false;

            PerformanceCounter pc = obj as PerformanceCounter;
            if (pc == null) return false;

            return (this._perfCount == pc._perfCount &&
                this._perfBase == pc._perfBase &&
                this._name == pc._name &&
                this._instance == pc._instance &&
                this._category == pc._category &&
                this._type == pc._type);
        }

        #endregion

        #region IDisposable Members

        // Implement IDisposable.
        // Do not make this method virtual.
        // A derived class should not be able to override this method.
        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        //========================================================================================================//
        // Dispose(bool disposing) executes in two distinct scenarios.
        // If disposing equals true, the method has been called directly
        // or indirectly by a user's code. Managed and unmanaged resources
        // can be disposed.
        // If disposing equals false, the method has been called by the 
        // runtime from inside the finalizer and you should not reference 
        // other objects. Only unmanaged resources can be disposed.
        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                // If disposing equals true, dispose all managed 
                // and unmanaged resources.
                if (disposing)
                {
                    // Dispose managed resources.
                    this.OnDisposeManagedResources();
                }

                // Call the appropriate methods to clean up 
                // unmanaged resources here.
                // If disposing is false, 
                // only the following code is executed.
                this.OnDisposeUnmanagedResources();
            }
            disposed = true;
        }

        /// <summary>
        /// Dispose managed resources
        /// </summary>
        protected virtual void OnDisposeManagedResources()
        {
            if (_perfBase != null)
            {
                if (_perfBase.InstanceName != null)
                    _perfBase.RemoveInstance();
                _perfBase.Close();
            }

            if (_perfCount != null)
            {
                if (_perfCount.InstanceName != null)
                    _perfCount.RemoveInstance();
                _perfCount.Close();
            }
        }

        /// <summary>
        /// Dispose unmanaged (native resources)
        /// </summary>
        protected virtual void OnDisposeUnmanagedResources()
        {
        }

        #endregion


        public bool ReadOnly { get; set; }
    }
}
