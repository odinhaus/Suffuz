using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz
{
    public static class ValueTypesEx
    {
        public static int Clamp(this int value, int min)
        {
            return value < min ? min : value;
        }

        public static int Clamp(this int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
