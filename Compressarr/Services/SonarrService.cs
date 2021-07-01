using Compressarr.Application;
using Compressarr.Helpers;
using Compressarr.Services.Base;
using Compressarr.Services.Models;
using Compressarr.Settings;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.Services
{
    public class SonarrService : ISonarrService
    {

        private readonly IApplicationService applicationService;
        private readonly IFileService fileService;
        private readonly ILogger<SonarrService> logger;

        private StatusResult _status = null;

        public SonarrService(IApplicationService applicationService, IFileService fileService, ILogger<SonarrService> logger)
        {
            this.applicationService = applicationService;
            this.fileService = fileService;
            this.logger = logger;
        }
        private IEnumerable<Series> Series => applicationService.Series;
        public string SeriesFilter { get; set; }
        public IEnumerable<string> SeriesFilterValues { get; set; }
        public async Task<ServiceResult<IEnumerable<Series>>> GetSeriesAsync(bool force = false)
        {
            using (logger.BeginScope("Get Series"))
            {
                logger.LogInformation($"Fetching Series");

                if (Series == null || !Series.Any() || force)
                {
                    var seriesRequest = await RequestSeries();
                    if (seriesRequest.Success)
                    {
                        applicationService.Series = seriesRequest.Results;
                    }
                    else
                    {
                        return seriesRequest;
                    }
                }

                if (!string.IsNullOrWhiteSpace(SeriesFilter))
                {
                    return new(true, FilterSeries(Series.JsonClone().AsQueryable(), SeriesFilter, SeriesFilterValues));
                }

                return new(true, Series);
            }

        }

        private IEnumerable<Series> FilterSeries(IQueryable<Series> series, string filter, IEnumerable<string> filterValues)
        {
            logger.LogDebug("Filtering Series");
            logger.LogDebug($"Filter: {filter}");
            logger.LogDebug($"Filter Values: {string.Join(", ", filterValues)}");

            return RecursiveFilter(series, filter, filterValues.ToArray()).Cast<Series>();
            
        }

        private IEnumerable RecursiveFilter(IEnumerable collection, string filter, string[] filterValues)
        {
            var reg = new Regex(@"(\w+\|)");

            if (reg.IsMatch(filter))
            {
                var match = reg.Match(filter);
                var prop = match.Value;

                filter = filter.Replace(prop, "");
                prop = prop.Replace("|", "");

                foreach (var ent in collection)
                {
                    ent.GetType().GetProperty(prop).SetValue(ent, RecursiveFilter(ent.GetType().GetProperty(prop).GetValue(ent) as IEnumerable, filter, filterValues));
                }

                return collection.AsQueryable().Where($"{prop}.Any()");
            }

            return collection.AsQueryable().Where(filter, filterValues);
        }

        public async Task<ServiceResult<IEnumerable<Series>>> GetSeriesFilteredAsync(string filter, IEnumerable<string> filterValues)
        {
            using (logger.BeginScope("Get Filtered Movies"))
            {
                logger.LogDebug("Filtering Movies");
                logger.LogDebug($"Filter: {filter}");
                logger.LogDebug($"Filter Values: {string.Join(", ", filterValues)}");
                SeriesFilter = filter;
                SeriesFilterValues = filterValues;
                return await GetSeriesAsync();
            }
        }

        public async Task<ServiceResult<IEnumerable<Series>>> RequestSeriesFilteredAsync(string filter, IEnumerable<string> filterValues)
        {
            var requestSeriesResponse = await RequestSeries();

            if (requestSeriesResponse.Success)
            {
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    return new(true, FilterSeries(requestSeriesResponse.Results.AsQueryable(), filter, filterValues));
                }

                return new(true, requestSeriesResponse.Results.AsQueryable());
            }
            return requestSeriesResponse;
        }

        public async Task<StatusResult> GetStatus()
        {
            if (_status == null)
            {
                _status = new();

                if (!string.IsNullOrWhiteSpace(applicationService.SonarrSettings?.APIURL))
                {
                    if ((await TestConnectionAsync(applicationService.SonarrSettings)).Success)
                    {
                        _status.Status = ServiceStatus.Ready;
                        _status.Message = new("Ready");
                    }
                    else
                    {
                        _status.Status = ServiceStatus.Partial;
                        _status.Message = new("Connection details are available, but connection cannot be completed. Check <a href=\"/options\">options</a>");
                    }
                }
                else
                {
                    _status.Status = ServiceStatus.Incomplete;
                    _status.Message = new("Connection details are missing. Use the <a href=\"/options\">options</a> page to enter them");

                }
            }

            return _status;
        }

        public async Task<ServiceResult<List<string>>> GetValuesForPropertyAsync(string property)
        {
            using (logger.BeginScope("Get Values for Property"))
            {
                logger.LogDebug($"Property name: {property}");

                if (Series.Any())
                {
                    var selectManySplit = property.Split("|");
                    IQueryable series = Series.AsQueryable();

                    for (int i = 0; i < selectManySplit.Length - 1; i++)
                    {
                        series = series.SelectMany(selectManySplit[i]);
                    }

                    return new ServiceResult<List<string>>(true, series.GroupBy(selectManySplit.Last()).OrderBy("Count() desc").ThenBy("Key").Select("Key").ToDynamicArray<string>().Where(x => !string.IsNullOrEmpty(x)).ToList());
                }

                var seriesResult = await RequestSeries();
                return new ServiceResult<List<string>>(seriesResult.Success, seriesResult.ErrorCode, seriesResult.ErrorMessage);
            }
        }

        public async Task<SystemStatus> TestConnectionAsync(APISettings settings)
        {
            using (logger.BeginScope("Test Connection"))
            {
                if (settings == null || !settings.Ok)
                {
                    logger.LogDebug("Test aborted, due to insufficient settings");
                    return new() { Success = false, ErrorMessage = "Sonarr Settings are missing" };
                }

                logger.LogInformation($"Test Sonarr Connection.");
                SystemStatus ss = new();

                var link = $"{settings.APIURL}/api/system/status?apikey={settings.APIKey}";
                logger.LogDebug($"LinkURL: {link}");
                var hc = new HttpClient();
                try
                {
                    logger.LogDebug($"Connecting.");
                    HttpResponseMessage hrm = await hc.GetAsync(link);
                    var statusJSON = await hrm.Content.ReadAsStringAsync();

                    if (hrm.IsSuccessStatusCode)
                    {
                        ss = JsonConvert.DeserializeObject<SystemStatus>(statusJSON);
                        ss.Success = true;
                        logger.LogInformation($"Success.");
                    }
                    else
                    {
                        logger.LogWarning($"Failed: {hrm.StatusCode}");
                        ss.Success = false;
                        ss.ErrorMessage = $"{hrm.StatusCode}";
                        if (hrm.ReasonPhrase != hrm.StatusCode.ToString())
                        {
                            ss.ErrorMessage += $"- {hrm.ReasonPhrase}";
                            logger.LogWarning($"Failed: {hrm.ReasonPhrase}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ss.Success = false;

                    ss.ErrorMessage = $"Request Exception: {ex.Message}";
                    logger.LogError(ex.ToString());
                }

                return ss;
            }
        }

        private async IAsyncEnumerable<EpisodeFile> GetEpisodeFiles(int seriesID)
        {
            using (logger.BeginScope($"Requesting Episode files for Series {seriesID}"))
            {
                var sonarrURL = applicationService.SonarrSettings?.APIURL;// settingsManager.Settings[SettingType.SonarrURL];
                var sonarrAPIKey = applicationService.SonarrSettings?.APIKey; // settingsManager.Settings[SettingType.SonarrAPIKey];

                var link = $"{sonarrURL}/api/episodefile?seriesid={seriesID}&apikey={sonarrAPIKey}";
                logger.LogDebug($"Link: {link}");

                var episodeJSON = string.Empty;

                HashSet<EpisodeFile> episodeFiles = new();

                try
                {
                    logger.LogDebug($"Creating new HTTP client.");
                    using (var hc = new HttpClient())
                    {
                        logger.LogDebug($"Downloading Series List.");
                        episodeJSON = await hc.GetStringAsync(link);

                        var fileArr = JsonConvert.DeserializeObject<EpisodeFile[]>(episodeJSON);

                        episodeFiles = fileArr.Where(f => !string.IsNullOrWhiteSpace(f.Path)).OrderBy(s => s.RelativePath).ToHashSet();

                        foreach(var epsf in episodeFiles)
                        {
                            epsf.BasePath = applicationService.SonarrSettings.BasePath;
                        }

                        logger.LogDebug($"Success.");
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        logger.LogError($"{ex}");

                        logger.LogWarning($"Error understanding output from Sonarr. Dumping output to: episode{seriesID}.json");

                        await fileService.DumpDebugFile($"episode{seriesID}.json", episodeJSON);
                    }
                    catch (Exception)
                    {
                        logger.LogCritical("Cannot dump debug file, permissions?");
                        logger.LogCritical(ex.ToString());
                    }
                    yield break;
                }

                foreach (var f in episodeFiles) yield return f;
            }
        }

        private async Task<ServiceResult<IEnumerable<Series>>> RequestSeries()
        {
            using (logger.BeginScope("Requesting Series"))
            {
                if (applicationService.SonarrSettings == null)
                {
                    logger.LogWarning($"No Sonarr settings.");
                    return new(false, "404", "Sonarr settings not found. In Options. Go there");
                }

                var sonarrURL = applicationService.SonarrSettings?.APIURL;// settingsManager.Settings[SettingType.SonarrURL];
                var sonarrAPIKey = applicationService.SonarrSettings?.APIKey; // settingsManager.Settings[SettingType.SonarrAPIKey];

                var link = $"{sonarrURL}/api/series?apikey={sonarrAPIKey}";
                logger.LogDebug($"Link: {link}");

                if (string.IsNullOrWhiteSpace(sonarrURL))
                {
                    logger.LogWarning($"No URL for Sonarr.");
                    return new(false, "404", "Sonarr URL not found. In Options. Go there");
                }

                if (string.IsNullOrWhiteSpace(sonarrAPIKey))
                {
                    logger.LogWarning($"No API key for Sonarr.");
                    return new(false, "404", "Sonarr APIKey not found. In Options. Go there");
                }

                var seriesJSON = string.Empty;

                try
                {
                    logger.LogDebug($"Creating new HTTP client.");
                    using (var hc = new HttpClient())
                    {
                        logger.LogDebug($"Downloading Series List.");
                        seriesJSON = await hc.GetStringAsync(link);

                        if (AppEnvironment.IsDevelopment)
                        {
                            _ = fileService.DumpDebugFile("series.json", seriesJSON);
                        }


                        var seriesArr = JsonConvert.DeserializeObject<Series[]>(seriesJSON);

                        var series = seriesArr.Where(s => s.EpisodeFileCount > 0).OrderBy(s => s.SortTitle).ToHashSet();

                        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

                        await series.AsyncParallelForEach(async s =>
                        {
                            await foreach (var ef in GetEpisodeFiles(s.Id))
                            {
                                (s.Seasons.FirstOrDefault(x => x.SeasonNumber == ef.SeasonNumber).EpisodeFiles as HashSet<EpisodeFile>).Add(ef);
                            }
                        }, 20, TaskScheduler.FromCurrentSynchronizationContext());


                        logger.LogDebug($"Success.");

                        return new(true, series);
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        logger.LogError($"{ex}");

                        logger.LogWarning($"Error understanding output from Sonarr. Dumping output to: series.json");

                        await fileService.DumpDebugFile("series.json", seriesJSON);
                    }
                    catch (Exception)
                    {
                        logger.LogCritical("Cannot dump debug file, permissions?");
                        logger.LogCritical(ex.ToString());
                    }

                    return new(false, null, ex.Message);
                }
            }

        }
    }
}