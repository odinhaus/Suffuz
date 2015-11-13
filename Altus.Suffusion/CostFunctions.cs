using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Altus.Suffusion
{
    public static class CostFunctions
    {
        public delegate double CapacityCostHandler(double currentValue, double maxValue);

        public static double CapacityCost(this double currentValue, double minValue = 0d, double maxValue = 100d)
        {
            //1 -\frac{ 1}
            //{ 1 + e ^{ -\left(\frac{ 15} { 10}\right)\left(x ^{ 0.9} -\ \frac{ 10} { 4}\right)} }

            /*
                                                      100
                f(x) = [1 - ---------------------------------------------------------]^2
                            100 + e^[-(15/maxValue)(currentValue^1.0 - (maxValue/1.2))]
            */
            return Math.Pow(1d - (1d / (1d + Math.Pow(Math.E, -(15d / maxValue) *( Math.Pow(currentValue, 0.99) - (maxValue / 1.2d))))), 2);
        }
    }
}
