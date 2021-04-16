using Compressarr.Filtering.Models;
using System.Collections.Generic;
using System.Linq;

namespace Compressarr.Filtering
{
    public interface IFilterManager
    {
        HashSet<Filter> Filters { get; }
        List<FilterComparitor> StringComparitors { get; }
        List<FilterComparitor> NumberComparitors { get; }
        List<FilterComparitor> EnumComparitors { get; }
        List<FilterComparitor> DateComparitors { get; }
        List<FilterProperty> RadarrFilterProperties { get; }
        List<FilterProperty> RadarrTableColumns { get; }

        void AddFilter(List<DynamicLinqFilter> dlFilters, string filterName, MediaSource filterType);
        string ConstructFilterQuery(List<DynamicLinqFilter> dlFilters, out string[] vals);
        void DeleteFilter(string filterName);
        List<FilterComparitor> GetComparitors(FilterProperty property);
        Filter GetFilter(string filterName);
        IOrderedEnumerable<Filter> GetFilters(MediaSource filterType);
    }
}