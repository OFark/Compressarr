using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Application.Interfaces
{
    public interface ICloneable<T>
    {
        T Clone();
    }
}
