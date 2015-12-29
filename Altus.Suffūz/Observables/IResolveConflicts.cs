using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public interface IResolveConflicts
    {
        T Resolve<T>(T currentState, ChangeState<T> localChanges, ChangeState<T> remoteChanges);
    }
}
