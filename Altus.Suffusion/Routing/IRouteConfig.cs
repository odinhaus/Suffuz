﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffusion.Routing
{
    public interface IRouteConfig
    {
        void Initialize(IServiceRouter router);
    }
}