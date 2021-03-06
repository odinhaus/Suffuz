﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Scheduling
{
    public class ScheduledDelegate : IScheduledTask
    {
        private ScheduledDelegate()
        {
            Id = Guid.NewGuid();
        }
        public ScheduledDelegate(Schedule schedule, Delegate executor) : this()
        {
            Schedule = schedule;
            Executor = executor;
            ExecuteArgs = new Func<object[]>(() => new object[0]);
        }

        public ScheduledDelegate(int interval, Delegate executor) : this()
        {
            Schedule = new PeriodicSchedule(DateRange.Forever, interval);
            Executor = executor;
            ExecuteArgs = new Func<object[]>(() => new object[0]);
        }

        public ScheduledDelegate(DateTime when, Delegate executor) : this()
        {
            Schedule = new FixedCalendricalSchedule(new DateRange(when, DateTime.MaxValue), 0, DateTime.MaxValue.Subtract(when));
            Executor = executor;
            ExecuteArgs = new Func<object[]>(() => new object[0]);
        }

        public ScheduledDelegate(Schedule schedule, Delegate executor, Func<object[]> executionArgs) : this()
        {
            Schedule = schedule;
            Executor = executor;
            ExecuteArgs = executionArgs;
        }

        public ScheduledDelegate(int interval, Delegate executor, Func<object[]> executionArgs) : this()
        {
            Schedule = new PeriodicSchedule(DateRange.Forever, interval);
            Executor = executor;
            ExecuteArgs = executionArgs;
        }
        public ScheduledDelegate(DateTime when, Delegate executor, Func<object[]> executionArgs) : this()
        {
            Schedule = new FixedCalendricalSchedule(new DateRange(when, DateTime.MaxValue), 0, DateTime.MaxValue.Subtract(when));
            Executor = executor;
            ExecuteArgs = executionArgs;
        }

        public Guid Id { get; private set; }
        public Delegate Executor { get; set; }

        public Func<object[]> ExecuteArgs
        {
            get;
            private set;
        }

        public Schedule Schedule
        {
            get;
            private set;
        }

        public object Execute(object[] args)
        {
            return Executor.DynamicInvoke(args);
        }

        public void Cancel()
        {
            Schedule.Cancel();
        }
    }
}
