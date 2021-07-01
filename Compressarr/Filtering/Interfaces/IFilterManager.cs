using Compressarr.Filtering.Models;
using Compressarr.Services.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Filtering
{
    public interface IFilterManager: IJobDependency
    {
        HashSet<Filter> Filters { get; }
        List<FilterComparitor> StringComparitors { get; }
        List<FilterComparitor> NumberComparitors { get; }
        List<FilterComparitor> EnumComparitors { get; }
        List<FilterComparitor> DateComparitors { get; }
        List<FilterProperty> RadarrFilterProperties { get; }
        List<FilterProperty> RadarrTableColumns { get; }

        List<FilterProperty> SonarrFilterProperties { get; }

        Task<Guid> AddFilter(List<DynamicLinqFilter> dlFilters, string filterName, MediaSource filterType);
        string ConstructFilterQuery(List<DynamicLinqFilter> dlFilters, out List<string> vals);
        Task DeleteFilter(string filterName);
        List<FilterComparitor> GetComparitors(FilterProperty property);
        Filter GetFilter(Guid id);
        IOrderedEnumerable<Filter> GetFilters(MediaSource filterType);
    }
}