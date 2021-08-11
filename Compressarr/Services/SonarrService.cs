using Compressarr.Application;
using Compressarr.Helpers;
using Compressarr.JobProcessing.Models;
using Compressarr.Services.Base;
using Compressarr.Services.Models;
using Compressarr.Settings;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

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

        public async Task<ServiceResult<object>> ImportEpisodeAsync(WorkItem workItem)
        {
            //Get ManualImport 
            //Request URL: /api/manualimport?folder=C%3A%5CCompressarr%5C&filterExistingFiles=true

            //Do Import
            //Request URL: /api/v3/command - /api/command may also work
            //FormData: {
            //  "name": "ManualImport",
            //  "files": [
            //    {
            //      "path": "/downloads/TV/Loki.S01E06.For.All.Time.Always.1080p.DSNP.WEB-DL.DDP5.1.H.264-EVO.mkv",
            //      "seriesId": 321,
            //      "episodeIds": [
            //        26601
            //      ],
            //      "quality": {
            //          "quality": {
            //              "id": 3,
            //              "name": "WEBDL-1080p",
            //              "source": "web",
            //              "resolution": 1080
            //           },
            //          "revision": {
            //              "version": 1,
            //              "real": 0,
            //              "isRepack": false
            //          }
            //      },
            //      "language": {
            //          "id": 1,
            //          "name": "English"
            //      }
            //   }
            //  ],
            //  "importMode": "copy"
            //}

            //Refresh
            //Request URL: /api/v3/command - /api/command may also work
            //FormData: {"name":"RefreshMovie","movieIds":[1]}:

            using (logger.BeginScope("Import Episode"))
            {
                logger.LogInformation("Asking Sonarr to validate imports");

                //We need the show folder not the season folder for Sonarr to recognise it.
                var destinationFolder = Path.GetDirectoryName(Path.GetDirectoryName(workItem.DestinationFile));

                var link = $"{applicationService.SonarrSettings?.APIURL}/api/manualimport?folder={HttpUtility.UrlEncode(destinationFolder)}&filterExistingFiles=true&apikey={applicationService.SonarrSettings?.APIKey}";
                logger.LogDebug($"Link: {link}");

                ManualImportEpisodeResponse mir = null;

                using (var hc = new HttpClient())
                {
                    try
                    {
                        logger.LogDebug("Requesting ManualImport");

                        var hrm = await hc.GetAsync(link);

                        var manualImportJSON = await hrm.Content.ReadAsStringAsync();

                        if (AppEnvironment.IsDevelopment)
                        {
                            _ = fileService.DumpDebugFile("manualImport.json", manualImportJSON).ConfigureAwait(false);
                        }

                        if (hrm.IsSuccessStatusCode)
                        {
                            var mirs = JsonConvert.DeserializeObject<HashSet<ManualImportEpisodeResponse>>(manualImportJSON);

                            mir = mirs.FirstOrDefault(x => (x?.Episodes?.Any(e => e.EpisodeFileId == workItem.SourceID) ?? false) && x?.Path == workItem.DestinationFile);

                            if (mir == null)
                            {
                                _ = fileService.DumpDebugFile("manualImport.json", manualImportJSON).ConfigureAwait(false);
                                logger.LogWarning($"Failed: Sonarr didn't recognise the file to import. SourceID: {workItem.SourceID}");
                                return new(false, "Failed", "Sonarr didn't recognise the file to import");
                            }

                            logger.LogInformation($"Success.");
                        }
                        else
                        {
                            _ = fileService.DumpDebugFile("manualImport.json", manualImportJSON).ConfigureAwait(false);
                            logger.LogWarning($"Failed: {hrm.StatusCode}");
                            if (hrm.ReasonPhrase != hrm.StatusCode.ToString())
                            {
                                logger.LogWarning($"Failed: {hrm.ReasonPhrase}");
                                return new(false, "Failed", hrm.ReasonPhrase);
                            }

                            return new(false, "Failed", hrm.StatusCode.ToString());
                        }

                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex.ToString());
                        return new ServiceResult<object>(false, "Exception", ex.ToString());
                    }
                }

                if (mir.Rejections.Any())
                {
                    logger.LogWarning($"Sonarr rejection [{mir.Rejections.FirstOrDefault().Type}] : {mir.Rejections.FirstOrDefault().Reason}");

                    return new(false, $"Sonarr rejection: {mir.Rejections.FirstOrDefault().Type}", mir.Rejections.FirstOrDefault().Reason);
                }

                logger.LogInformation("Importing into Sonarr");

                var payload = new ImportEpisodePayload();


                logger.LogDebug("Get FileInfo");

                //var encodedFile = new FileInfo(workItem.DestinationFile);

                var file = new ImportEpisodePayload.File()
                {
                    EpisodeIds = mir.Episodes.Where(e => e.EpisodeFileId == workItem.SourceID).Select(e => e.Id).ToList(),
                    Language = new Language() { Id = 1, Name = "English" },
                    Path = mir.Path,
                    Quality = mir.Quality,
                    SeriesId = mir.Series.Id
                };

                payload.Files = new() { file };

                link = $"{applicationService.SonarrSettings?.APIURL}/api/command?apikey={applicationService.SonarrSettings?.APIKey}";
                logger.LogDebug($"Link: {link}");


                using (var hc = new HttpClient())
                {
                    try
                    {
                        logger.LogDebug("POSTing payload");

                        var payloadJson = JsonConvert.SerializeObject(payload);

                        var result = await hc.PostAsync(link, new StringContent(payloadJson, Encoding.UTF8, "application/json"));

                        if (AppEnvironment.IsDevelopment)
                        {
                            _ = fileService.DumpDebugFile("importEpisodePayload.json", payloadJson);
                            _ = fileService.DumpDebugFile("importEpisodeResponse.json", await result.Content.ReadAsStringAsync());
                        }

                        if (result.IsSuccessStatusCode)
                        {
                            logger.LogDebug("Success");
                            ClearCache();
                            return new ServiceResult<object>(true, true);
                        }
                        else
                        {
                            logger.LogWarning($"Failed: {result.ReasonPhrase}");
                            _ = fileService.DumpDebugFile("importEpisodePayload.json", payloadJson);
                            _ = fileService.DumpDebugFile("importEpisodeResponse.json", await result.Content.ReadAsStringAsync());
                            return new ServiceResult<object>(false, result.StatusCode.ToString(), result.ReasonPhrase);
                        }

                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex.ToString());
                        return new ServiceResult<object>(false, "Exception", ex.ToString());
                    }
                }
            }
        }

        public void ClearCache()
        {
            applicationService.Series = new HashSet<Series>();
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

        private IEnumerable<Series> FilterSeries(IQueryable<Series> series, string filter, IEnumerable<string> filterValues)
        {
            logger.LogDebug("Filtering Series");
            logger.LogDebug($"Filter: {filter}");
            logger.LogDebug($"Filter Values: {string.Join(", ", filterValues)}");

            return RecursiveFilter(series, filter, filterValues.ToArray()).Cast<Series>();
            
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
                    using var hc = new HttpClient();

                    logger.LogDebug($"Downloading Series List.");
                    episodeJSON = await hc.GetStringAsync(link);

                    var fileArr = JsonConvert.DeserializeObject<EpisodeFile[]>(episodeJSON);

                    episodeFiles = fileArr.Where(f => !string.IsNullOrWhiteSpace(f.Path)).OrderBy(s => s.RelativePath).ToHashSet();

                    foreach (var epsf in episodeFiles)
                    {
                        epsf.BasePath = applicationService.SonarrSettings.BasePath;
                    }

                    logger.LogDebug($"Success.");
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

        private IEnumerable RecursiveFilter(IEnumerable collection, string filter, string[] filterValues)
            => collection.AsQueryable().Where(filter, filterValues).ToDynamicList<Series>();

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
                    using var hc = new HttpClient();

                    logger.LogDebug($"Downloading Series List.");
                    seriesJSON = await hc.GetStringAsync(link);

                    if (AppEnvironment.IsDevelopment)
                    {
                        _ = fileService.DumpDebugFile("series.json", seriesJSON);
                    }

                    var series = new ConcurrentBag<Series>();

                    var seriesArr = JsonConvert.DeserializeObject<SeriesJSON[]>(seriesJSON);

                    var seriesWithFiles = seriesArr.Where(s => s.EpisodeFileCount > 0).OrderBy(s => s.SortTitle).ToHashSet();

                    SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

                    await seriesWithFiles.AsyncParallelForEach(async s =>
                    {
                        await foreach (var ef in GetEpisodeFiles(s.Id))
                        {
                            //(s.Seasons.FirstOrDefault(x => x.SeasonNumber == ef.SeasonNumber).EpisodeFiles as HashSet<EpisodeFile>).Add(ef);

                            var ser = new Series(s, new Season(s.Seasons.FirstOrDefault(x => x.SeasonNumber == ef.SeasonNumber), ef));
                            series.Add(ser);
                        }
                    }, 20, TaskScheduler.FromCurrentSynchronizationContext());


                    logger.LogDebug($"Success.");

                    return new(true, series.OrderBy(x => x.Title).ThenBy(x => x.Season.SeasonNumber).ThenBy(x => x.Season.EpisodeFile.Path));
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