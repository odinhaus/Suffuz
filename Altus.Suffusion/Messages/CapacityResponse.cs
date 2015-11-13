﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Messages
{
    public class CapacityResponse<TRequest>
    {
        public double Minimum { get; set; }
        public double Maximum { get; set; }
        public double Current { get; set; }
        public double Score { get; set; }
    }
}
