using Compressarr.Filtering.Models;
using Compressarr.Helpers;
using Compressarr.Services.Base;
using Compressarr.Services.Models;
using Compressarr.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections;

namespace Compressarr.Filtering
{
    public class FilterManager : IFilterManager
    {
        private readonly ILogger<FilterManager> logger;
        private readonly IApplicationService settingsManager;
        public FilterManager(ILogger<FilterManager> logger, IApplicationService settingsManager)
        {
            this.logger = logger;
            this.settingsManager = settingsManager;

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

            RadarrFilterProperties = BuildFilterProperties(typeof(Movie));
            SonarrFilterProperties = BuildFilterProperties(typeof(Series));

            //This HAS to come after building the filter properties.
            RadarrTableColumns = GetRadarrTableColumns();
        }

        public List<FilterComparitor> DateComparitors { get; }
        public List<FilterComparitor> EnumComparitors { get; }
        public HashSet<Filter> Filters => settingsManager.Filters;
        public List<FilterComparitor> NumberComparitors { get; }
        public List<FilterProperty> RadarrFilterProperties { get; }
        public List<FilterProperty> RadarrTableColumns { get; private set; }
        public List<FilterComparitor> StringComparitors { get; }

        public List<FilterProperty> SonarrFilterProperties { get; }

        public async Task<Guid> AddFilter(List<DynamicLinqFilter> dlFilters, string filterName, MediaSource filterType)
        {
            using (logger.BeginScope("Add Filter"))
            {
                logger.LogInformation($"Filter name: {filterName}");

                var filter = Filters.FirstOrDefault(x => x.Name == filterName);

                if (filter == null)
                {
                    logger.LogDebug($"Filter not found, creating a new one.");

                    filter = new Filter(filterName, filterType);
                    Filters.Add(filter);
                }

                filter.Filters = dlFilters.JsonClone();

                await settingsManager.SaveAppSetting();

                return filter.ID;
            }
        }

        public string ConstructFilterQuery(List<DynamicLinqFilter> dlFilters, out List<string> vals)
        {
            var filtervalues = new List<string>();
            var filterStr = RecursiveFilterQuery(dlFilters, ref filtervalues);

            vals = filtervalues;

            return filterStr;
        }

        public Task DeleteFilter(string filterName)
        {
            logger.LogDebug($"Deleting Filter ({filterName}).");

            var filter = Filters.FirstOrDefault(x => x.Name == filterName);

            if (filter != null)
            {
                Filters.Remove(filter);
            }
            else
            {
                logger.LogWarning($"Filter not found.");
            }

            return settingsManager.SaveAppSetting();
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

        public Filter GetFilter(Guid id) => Filters.FirstOrDefault(x => x.ID == id);

        /// <summary>
        /// Gets all filters matching the selected source
        /// </summary>
        /// <param name="filterType"></param>
        /// <returns></returns>
        public IOrderedEnumerable<Filter> GetFilters(MediaSource filterType) => Filters.Where(x => x.MediaSource == filterType).OrderBy(x => x.Name);

        public Task<StatusResult> GetStatus()
        {
            return Task.Run(() =>
            {
                return new StatusResult() { Status = Filters.Any() ? ServiceStatus.Ready : ServiceStatus.Incomplete, Message = new(Filters.Any() ? "Ready" : "No filters have been defined, you can create some on the <a href=\"/radarr\">Radarr</a> or <a href=\"/sonarr\">Sonarr</a> page") };
            });
        }

        private List<FilterProperty> BuildFilterProperties(Type type, List<FilterProperty> filterProperties = null, string name = null, string prefix = null)
        {
            filterProperties ??= new();
            var enumerable = false;

            if (type.GetInterfaces().Contains(typeof(IEnumerable)))
            {
                type = type.GetGenericArguments()[0];
                enumerable = true;
            }

            foreach (var prop in type.GetProperties())
            {
                if (prop.GetCustomAttributes(false).FirstOrDefault(a => a is FilterAttribute) is FilterAttribute attr)
                {
                    var attrName = string.IsNullOrWhiteSpace(name) ? attr.Name : $"{name} - {attr.Name}";
                    var attrPrefix = string.IsNullOrWhiteSpace(prefix) ? prop.Name : $"{prefix}{(enumerable ? "|": ".")}{prop.Name}";
                    var attrfilterOn = string.IsNullOrWhiteSpace(attr.FilterOn) ? attrPrefix : string.IsNullOrWhiteSpace(prefix) ? attr.FilterOn : $"{prefix}{(enumerable ? "|" : ".")}{attr.FilterOn}";

                    if (attr.Traverse)
                    {
                        BuildFilterProperties(prop.PropertyType, filterProperties, attrName, attrPrefix);
                    }
                    else
                    {
                        filterProperties.Add(new FilterProperty(attrName, attrPrefix, attr.PropertyType, attr.Suffix, attrfilterOn));
                    }
                }
            }

            return filterProperties;
        }
        private List<FilterProperty> GetRadarrTableColumns()
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
            return columns.Join(RadarrFilterProperties.Where(f => columns.Contains(f.Value)), c => c, f => f.Value, (c, f) => f).ToList();
        }

        private string RecursiveFilterQuery(List<DynamicLinqFilter> dlFilters, ref List<string> vals)
        {
            var sb = new StringBuilder();

            foreach (var dlFilter in dlFilters)
            {
                if (dlFilter.IsGroup)
                {
                    sb.Append($" {dlFilter.LogicalOperator} ({RecursiveFilterQuery(dlFilter.SubFilters, ref vals)})");
                }
                else
                {
                    string filterStr;
                    if (dlFilter.Comparitor.IsParamMethod)
                    {
                        var nullPropReg = new Regex(@"\|?([\w\.]+)(?!\|)+$");

                        var valStr = nullPropReg.Replace(dlFilter.Property.Value, "|np($1");

                        filterStr = $" {dlFilter.LogicalOperator} {valStr}{dlFilter.Comparitor.Operator},false)";

                        vals.Add(dlFilter.Value);
                        var reg = new Regex(@"\(@\)");
                        filterStr = reg.Replace(filterStr, $"(@{vals.Count - 1})");
                    }
                    else
                    {
                        filterStr = dlFilter.Property.PropertyType switch
                        {
                            var x when
                            x == FilterPropertyType.DateTime ||
                            x == FilterPropertyType.String => $" {dlFilter.LogicalOperator} {dlFilter.Property.Value}{dlFilter.Comparitor.Operator}\"{dlFilter.Value}\"",
                            FilterPropertyType.Enum => $" {dlFilter.LogicalOperator} ( {string.Join(dlFilter.Comparitor.Operator == " == " ? " or " : " and ", (dlFilter.Values ?? new HashSet<string> { dlFilter.Value }).Select(x => $"{dlFilter.Property.Value}{dlFilter.Comparitor.Operator}\"{x}\""))} )",
                            _ => filterStr = $" {dlFilter.LogicalOperator} {dlFilter.Property.Value}{dlFilter.Comparitor.Operator}{dlFilter.Value}"
                        };
                    }

                    sb.Append(filterStr);
                }
            }

            return sb.ToString().Trim();
        }
    }
}