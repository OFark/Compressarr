using Compressarr.Filtering.Models;
using Compressarr.Helpers;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Compressarr.Filtering
{
    public class FilterManager
    {
        public static readonly List<FilterComparitor> StringComparitors;
        public static readonly List<FilterComparitor> NumberComparitors;
        public static readonly List<FilterComparitor> EnumComparitors;

        public static readonly List<FilterProperty> FilterProperties;

        private string filterFilePath => Path.Combine(_env.ContentRootPath, "config", "filters.json");

        private HashSet<Filter> _filters { get; set; }
        public HashSet<Filter> Filters => _filters ?? LoadFilters();

        private IWebHostEnvironment _env;

        public FilterManager(IWebHostEnvironment env)
        {
            _env = env;
        }

        static FilterManager()
        {
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

            FilterProperties = new List<FilterProperty>()
            {
                new FilterProperty("title", FilterPropertyType.String),
                new FilterProperty("movieFile.mediaInfo.width", FilterPropertyType.Number),
                new FilterProperty("movieFile.mediaInfo.height", FilterPropertyType.Number),
                new FilterProperty("movieFile.mediaInfo.videoCodec", FilterPropertyType.Enum),
                new FilterProperty("movieFile.mediaInfo.videoCodecID", FilterPropertyType.String),
                new FilterProperty("movieFile.mediaInfo.videoFormat", FilterPropertyType.Enum),
                new FilterProperty("movieFile.mediaInfo.containerFormat", FilterPropertyType.Enum),
                new FilterProperty("movieFile.mediaInfo.videoBitDepth", FilterPropertyType.Number),
                new FilterProperty("movieFile.mediaInfo.videoBitrate", FilterPropertyType.Number),
                new FilterProperty("movieFile.mediaInfo.videoFps", FilterPropertyType.Number),
                new FilterProperty("movieFile.mediaInfo.videoDataRate", FilterPropertyType.Number),
                new FilterProperty("movieFile.mediaInfo.audioFormat", FilterPropertyType.String),
                new FilterProperty("movieFile.mediaInfo.audioCodecID", FilterPropertyType.Number)
            };
        }

        public string ConstructFilterQuery(List<DynamicLinqFilter> dlFilters, out string[] vals)
        {
            var filtervalues = new List<string>();
            var filterStr = recursiveFilterQuery(dlFilters, ref filtervalues);

            vals = filtervalues.ToArray();

            return filterStr;
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
                    else if (dlFilter.Property.PropertyType == FilterPropertyType.Enum || dlFilter.Property.PropertyType == FilterPropertyType.String)
                    {
                        filterStr = $" {dlFilter.LogicalOperator} {dlFilter.Property.Value}{dlFilter.Comparitor.Operator}\"{dlFilter.Value}\"";
                    }
                    else
                    {
                        filterStr = $" {dlFilter.LogicalOperator} {dlFilter.Property.Value}{dlFilter.Comparitor.Operator}{dlFilter.Value}";
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