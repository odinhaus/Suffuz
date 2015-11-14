using Altus.Suffūz.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Altus.Suffūz
{
    public static class CurrentTime
    {
        static System.DateTime _base = CurrentTime.Now;

        static CurrentTime()
        {
            Thread t = new Thread(delegate ()
            {
                while (true)
                {
                    Thread.Sleep(100);
                    Time = MetricsHelper.CurrentTime();
                }
            });
            t.IsBackground = true;
            t.Name = "Current Time Updater";
            t.Start();

            Initialize();
        }

        public static double Time { get; private set; }
        public static System.DateTime Now { get { Initialize(); return _base.AddTicks((long)((MetricsHelper.CurrentTime() - _baseTS) * TimeSpan.TicksPerMillisecond)); } }
        private static bool _isInitialized = false;
        private static double _baseTS;
        private static void Initialize()
        {
            if (!_isInitialized)
            {
                _base = System.DateTime.Now;
                _baseTS = MetricsHelper.CurrentTime();
                _isInitialized = true;
            }
        }
    }
}
