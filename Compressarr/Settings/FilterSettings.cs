using Compressarr.Filtering.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Settings
{
    public class FilterSettings
    {
        public HashSet<Filter> Filters { get;  set; }
    }
}
