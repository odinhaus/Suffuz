using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Observables
{
    public class MultiplicativeConflictResolver : IResolveConflicts
    {
        public T Resolve<T>(T currentState, ChangeState<T> localChanges, ChangeState<T> remoteChanges)
        {
            throw new NotImplementedException();
        }
    }
}
