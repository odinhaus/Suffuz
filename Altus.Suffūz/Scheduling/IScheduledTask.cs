using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Altus.Suffūz.Scheduling
{
    public interface IScheduledTask
    {
        Guid Id { get; }
        object Execute(object[] args);
        Schedule Schedule { get; }
        Func<object[]> ExecuteArgs { get; }
        void Cancel();
    }
}
