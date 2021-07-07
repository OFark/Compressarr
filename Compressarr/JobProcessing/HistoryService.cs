using Compressarr.Application;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets.Models;
using LiteDB;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.JobProcessing
{
    public class HistoryService : IHistoryService
    {
        private const string HISTORYTABLE = "History";
        private readonly IFileService fileService;
        private readonly ILogger<HistoryService> logger;
        public HistoryService(IFileService fileService, ILogger<HistoryService> logger)
        {
            this.fileService = fileService;

            this.logger = logger;
        }

        public async Task ClearAutoCalcResult(int mediaInfoID)
        {
            await Task.Run(() =>
            {
                using (logger.BeginScope("GetAutoCalcResults"))
                {
                    using var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo));
                    var results = db.GetCollection<AutoCalcResult>();
                    results.DeleteMany(x => x.MediaInfoID == mediaInfoID);
                }
            });
        }

        public void EndProcessing(Guid historyEntryID, bool succeeded, WorkItem workItem)
        {
            using var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo));

            try
            {
                var histories = db.GetCollection<History>();
                var history = histories.Query().Include(x => x.Entries).Where(x => x.MediaID == workItem.Media.UniqueID).FirstOrDefault();

                if (history != null)
                {
                    var historyProcessing = history.Entries.FirstOrDefault(x => x.HistoryID == historyEntryID);

                    historyProcessing.Finished = DateTime.Now;
                    historyProcessing.Success = succeeded;
                    historyProcessing.Compression = workItem.Compression;
                    historyProcessing.FPS = workItem.FPS;
                    historyProcessing.Percentage = workItem.Percent;
                    historyProcessing.Speed = workItem.Speed;
                    historyProcessing.SSIM = workItem.SSIM;

                    histories.Update(history);
                }
            }
            catch (InvalidCastException)
            {
                db.DropCollection(HISTORYTABLE);
            }
        }

        public async Task<AutoCalcResult> GetAutoCalcResult(int mediaInfoID, string argument, int sampleLength)
        {
            return await Task.Run(() =>
            {
                using (logger.BeginScope("GetAutoCalcResults"))
                {
                    using var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo));
                    var results = db.GetCollection<AutoCalcResult>();
                    var result = results.Query().Where(x => x.MediaInfoID == mediaInfoID && x.Argument == argument.Trim() && x.SampleLength == sampleLength).FirstOrDefault();

                    return result;
                }
            });
        }

        public async Task<SortedSet<HistoryProcessing>> GetProcessHistoryAsync(int mediaID)
        {
            using var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo));

            return await Task.Run(() =>
            {
                try
                {
                    var histories = db.GetCollection<History>();
                    var history = histories.Query().Where(x => x.MediaID == mediaID).FirstOrDefault();
                    return new SortedSet<HistoryProcessing>(history?.Entries ?? new());
                }
                catch (InvalidCastException)
                {
                    db.DropCollection(HISTORYTABLE);
                    return new();
                }
            });
        }

        public Guid StartProcessing(int mediaID, string filePath, Guid filterID, string preset, IEnumerable<string> arguments)
        {
            using var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo));

            var history = new History();
            ILiteCollection<History> histories = default;

            try
            {
                histories = db.GetCollection<History>();
                history = histories.Query().Include(x => x.Entries).Where(x => x.MediaID == mediaID).FirstOrDefault();
            }
            catch (InvalidCastException)
            {
                db.DropCollection(HISTORYTABLE);
                histories = db.GetCollection<History>();
                //todo: report on screen
            }

            if (history == default)
            {
                history = new History()
                {
                    MediaID = mediaID
                };
                histories.Insert(history);
            }

            history.Entries ??= new();

            var historyProcessing = new HistoryProcessing()
            {
                Arguments = arguments.ToList(),
                FilePath = filePath,
                FilterID = filterID,
                HistoryID = Guid.NewGuid(),
                Preset = preset,
                Started = DateTime.Now,
                Type = "Processed"
            };

            history.Entries.Add(historyProcessing);

            histories.EnsureIndex(x => x.Id);
            histories.EnsureIndex(x => x.Entries);

            histories.Update(history);

            return historyProcessing.HistoryID;
        }
    }
}
