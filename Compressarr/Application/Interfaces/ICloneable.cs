using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Application
{
    public interface ICloneable<T>
    {
        T Clone();
    }
}
