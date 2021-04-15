using Compressarr.Filtering.Models;
using Compressarr.Helpers;
using Compressarr.Services.Models;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Compressarr.Filtering
{
    public class FilterManager
    {
        public readonly List<FilterComparitor> StringComparitors;
        public readonly List<FilterComparitor> NumberComparitors;
        public readonly List<FilterComparitor> EnumComparitors;
        public readonly List<FilterComparitor> DateComparitors;

        public readonly List<FilterProperty> FilterProperties;

        private string filterFilePath => Path.Combine(_env.ContentRootPath, "config", "filters.json");

        private HashSet<Filter> _filters { get; set; }
        public HashSet<Filter> Filters => _filters ?? LoadFilters();

        private IWebHostEnvironment _env;

        public FilterManager(IWebHostEnvironment env)
        {
            _env = env;

            DateComparitors = new List<FilterComparitor>()
            {
                new FilterComparitor("<"),
                new FilterComparitor(">")
            };

            StringComparitors = new List<FilterComparitor>()
            {
                new FilterComparitor("=="),
                new FilterComparitor("!="),
                new FilterComparitor("Contains"),
                new FilterComparitor("!Contains")
            };

            NumberComparitors = new List<FilterComparitor>()
            {
                new FilterComparitor("=="),
                new FilterComparitor("!="),
                new FilterComparitor("<"),
                new FilterComparitor(">"),
                new FilterComparitor("<="),
                new FilterComparitor(">=")
            };

            EnumComparitors = new List<FilterComparitor>()
            {
                new FilterComparitor("=="),
                new FilterComparitor("!=")
            };

            FilterProperties = new List<FilterProperty>();

            buildFilterProperties();
        }

        private void buildFilterProperties(Type type = null, string name = null, string prefix = null)
        {
            foreach (var prop in (type ?? typeof(Services.Models.Movie)).GetProperties())
            {
                var attr = prop.GetCustomAttributes(false).FirstOrDefault(a => a is FilterAttribute) as FilterAttribute;
                if (attr != null)
                {
                    var attrName = string.IsNullOrWhiteSpace(name) ? attr.Name : $"{name} - {attr.Name}";
                    var attrPrefix = string.IsNullOrWhiteSpace(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                    var attrfilterOn = string.IsNullOrWhiteSpace(attr.FilterOn) ? attrPrefix : string.IsNullOrWhiteSpace(prefix) ? attr.FilterOn : $"{prefix}.{attr.FilterOn}";

                    if (attr.Traverse)
                    {
                        buildFilterProperties(prop.PropertyType, attrName, attrPrefix);
                    }
                    else
                    {
                        FilterProperties.Add(new FilterProperty(attrName, attrPrefix, attr.PropertyType, attr.Suffix, attrfilterOn));
                    }
                }
            }
        }

        public string ConstructFilterQuery(List<DynamicLinqFilter> dlFilters, out string[] vals)
        {
            var filtervalues = new List<string>();
            var filterStr = recursiveFilterQuery(dlFilters, ref filtervalues);

            vals = filtervalues.ToArray();

            return filterStr;
        }

        public List<FilterComparitor> GetComparitors(FilterProperty property)
        {
            if (property != null)
            {
                switch (property.PropertyType)
                {
                    case FilterPropertyType.Boolean:
                        return new List<FilterComparitor>() { new FilterComparitor("==") };

                    case FilterPropertyType.Enum:
                        return EnumComparitors;

                    case FilterPropertyType.Number:
                        return NumberComparitors;

                    case FilterPropertyType.DateTime:
                        return DateComparitors;

                    case FilterPropertyType.String:
                        return StringComparitors;
                }
            }

            return StringComparitors;
        }

        public List<FilterProperty> GetTableColumns()
        {
            List<string> columns = new()
            {
                "title",
                "movieFile.mediaInfo.width",
                "movieFile.mediaInfo.height",
                "movieFile.mediaInfo.videoCodec",
                "movieFile.mediaInfo.videoFormat",
                "movieFile.mediaInfo.containerFormat",
                "movieFile.mediaInfo.videoBitDepth",
                "movieFile.mediaInfo.videoColourPrimaries",
                "movieFile.mediaInfo.videoBitrateNice",
                "movieFile.mediaInfo.videoFps",
                "movieFile.mediaInfo.audioFormat",
                "movieFile.mediaInfo.audioCodecID",
                "movieFile.mediaInfo.runTimeNice",
                "movieFile.mediaInfo.videoDataRate",
                "movieFile.sizeNice"
            };

            //The join here ensures the order
            return columns.Join(FilterProperties.Where(f => columns.Contains(f.Value)), c => c, f => f.Value, (c,f) => f).ToList();
        }

        private string recursiveFilterQuery(List<DynamicLinqFilter> dlFilters, ref List<string> vals)
        {
            var sb = new StringBuilder();

            foreach (var dlFilter in dlFilters)
            {
                if (dlFilter.IsGroup)
                {
                    sb.Append($" {dlFilter.LogicalOperator} ({recursiveFilterQuery(dlFilter.SubFilters, ref vals)})");
                }
                else
                {
                    string filterStr;
                    if (dlFilter.Comparitor.IsParamMethod)
                    {
                        filterStr = $" {dlFilter.LogicalOperator} {dlFilter.Property.Value}{dlFilter.Comparitor.Operator}";
                    }
                    else
                    {
                        switch (dlFilter.Property.PropertyType)
                        {
                            case FilterPropertyType.DateTime:
                            case FilterPropertyType.Enum:
                            case FilterPropertyType.String:
                                {
                                    filterStr = $" {dlFilter.LogicalOperator} {dlFilter.Property.Value}{dlFilter.Comparitor.Operator}\"{dlFilter.Value}\"";
                                }
                                break;
                            default:
                                {
                                    filterStr = $" {dlFilter.LogicalOperator} {dlFilter.Property.Value}{dlFilter.Comparitor.Operator}{dlFilter.Value}";
                                }
                                break;
                        }
                    }

                    if (dlFilter.Comparitor.IsParamMethod)
                    {
                        vals.Add(dlFilter.Value);
                        var reg = new Regex(@"\(@\)");
                        filterStr = reg.Replace(filterStr, $"(@{vals.Count() - 1})");
                    }

                    sb.Append(filterStr);
                }
            }

            return sb.ToString().Trim();
        }

        public void AddFilter(List<DynamicLinqFilter> dlFilters, string filterName, MediaSource filterType)
        {
            var filter = Filters.FirstOrDefault(x => x.Name == filterName);

            if (filter == null)
            {
                filter = new Filter(filterName, filterType);
                _filters.Add(filter);
            }

            filter.Filters = dlFilters.Clone();

            SaveFilters();
        }

        public void DeleteFilter(string filterName)
        {
            var filter = Filters.FirstOrDefault(x => x.Name == filterName);

            if (filter != null)
            {
                _filters.Remove(filter);
            }

            SaveFilters();
        }

        public Filter GetFilter(string filterName) => Filters.FirstOrDefault(x => x.Name == filterName);

        /// <summary>
        /// Gets all filters matching the selected source
        /// </summary>
        /// <param name="filterType"></param>
        /// <returns></returns>
        public IOrderedEnumerable<Filter> GetFilters(MediaSource filterType) => Filters.Where(x => x.MediaSource == filterType).OrderBy(x => x.Name);

        private HashSet<Filter> LoadFilters()
        {
            _filters = new HashSet<Filter>();

            if (File.Exists(filterFilePath))
            {
                var json = File.ReadAllText(filterFilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    _filters = JsonConvert.DeserializeObject<HashSet<Filter>>(json);
                }
            }

            return _filters;
        }

        private void SaveFilters()
        {
            var json = JsonConvert.SerializeObject(Filters, new JsonSerializerSettings() { Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore });

            if (!Directory.Exists(Path.GetDirectoryName(filterFilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filterFilePath));
            }

            File.WriteAllText(filterFilePath, json);
        }
    }
}