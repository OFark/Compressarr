using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Services.Base
{
    public class IgnoreOrderComparer : IEqualityComparer<IList<string>>
    {
        public IgnoreOrderComparer(StringComparer comparer)
        {
            this.Comparer = comparer;
        }

        public StringComparer Comparer { get; set; }

        public bool Equals(IList<string> x, IList<string> y)
        {
            if (x == null || y == null) return false;
            // remove the Distincts if there are never duplicates as mentioned
            return !x.Distinct(Comparer).Except(y.Distinct(Comparer), Comparer).Any();
            // btw, this should work if the order matters:
            // return x.SequenceEqual(y, Comparer);
        }

        public int GetHashCode(IList<string> arr)
        {
            if (arr == null) return int.MinValue;
            int hash = 19;
            foreach (string s in arr.Distinct(Comparer))
            {
                hash = hash + s.GetHashCode();
            }
            return hash;
        }
    }
}
