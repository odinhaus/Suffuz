using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Diagnostics
{
    /// <summary>
    /// Performance Counter install attribute that assists in creation of runtime counters
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = true)]
    public class PerformanceCounterInstallAttribute : Attribute
    {
        /// <summary>
        /// The counter type
        /// </summary>
        private PerformanceCounterType _type = PerformanceCounterType.NumberOfItems32;

        /// <summary>
        /// The counter lifetype scope - default global
        /// </summary>
        private PerformanceCounterInstanceLifetime _lifeTime = PerformanceCounterInstanceLifetime.Global;

        /// <summary>
        /// The category type - default multiinstance
        /// </summary>
        private PerformanceCounterCategoryType _categoryType = PerformanceCounterCategoryType.MultiInstance;

        /// <summary>
        /// by default, perf counters are created during startup/install
        /// </summary>
        private CounterInstanceType _counterInstanceType = CounterInstanceType.Static;

        /// <summary>
        /// The counters help/description
        /// </summary>
        private string _counterHelp;

        /// <summary>
        /// The counters category
        /// </summary>
        private PerformanceCounterCategories _category;

        /// <summary>
        /// The counters name
        /// </summary>
        private string _counterName;


        private string _instanceType = "Agent";


        /// <summary>
        /// Create a Performance counter install attribute to assist in run-time perfmon counter creation
        /// </summary>
        /// <param name="type">This counters internal type.  Needed base types are automagically created with the name appended with 'base'</param>
        /// <param name="category">This counters category.  All categories are fixed at the time of creation</param>
        /// <param name="lifetime">This counters lifetime, by default all counters are global except for dynamically created instances</param>
        /// <param name="categoryType">The category type (multi or single instance)</param>
        /// <param name="ConnectInstancetype">The CSGO instance type </param>
        /// <param name="counterName">This counters name</param>
        /// <param name="counterHelp">This counters help string</param>
        public PerformanceCounterInstallAttribute(
            PerformanceCounterType type,
            PerformanceCounterCategories category,
            PerformanceCounterInstanceLifetime lifetime,
            PerformanceCounterCategoryType categoryType,
            CounterInstanceType counterInstancetype,
            string counterName,
            string counterHelp,
            string instanceType)
        {
            _type = type;
            _category = category;
            _lifeTime = lifetime;
            _categoryType = categoryType;
            _counterInstanceType = counterInstancetype;
            _counterName = counterName;
            _counterHelp = counterHelp;
            _instanceType = instanceType;
        }

        /// <summary>
        /// returns the CSGO instance type
        /// </summary>
        public CounterInstanceType ConnectInstanceType
        {
            get { return _counterInstanceType; }
        }

        public string InstanceType
        {
            get { return _instanceType; }
        }

        /// <summary>
        /// Lifetime of counter to install
        /// </summary>
        public PerformanceCounterInstanceLifetime PerformanceCounterInstanceLifetime
        {
            get { return _lifeTime; }
        }

        /// <summary>
        /// Type of counter to install
        /// </summary>
        public PerformanceCounterType PerformanceCounterType
        {
            get { return _type; }
        }

        /// <summary>
        /// Counter category to install into
        /// </summary>
        public PerformanceCounterCategories Category
        {
            get { return _category; }
        }

        public PerformanceCounterCategoryType CategoryType
        {
            get { return _categoryType; }
        }

        /// <summary>
        /// Name of the counter to install
        /// </summary>
        public string Name
        {
            get { return _counterName; }
        }

        /// <summary>
        /// Help on this counter
        /// </summary>
        public string CounterHelp
        {
            get { return _counterHelp; }
        }
    }
}
