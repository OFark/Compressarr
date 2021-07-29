using Compressarr.Application;
using Compressarr.History.Models;
using Compressarr.JobProcessing.Models;
using Compressarr.Presets.Models;
using LiteDB;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HistoryEntry = Compressarr.History.Models.HistoryEntry;

namespace Compressarr.History
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
                var historyEntries = db.GetCollection<HistoryEntry>();
                var entry = historyEntries.Query().Where(x => x.HistoryID == historyEntryID).FirstOrDefault();

                if (entry != null)
                {
                    entry.Finished = DateTime.Now;
                    entry.ProcessingHistory.Success = succeeded;
                    entry.ProcessingHistory.Compression = workItem.Compression;
                    entry.ProcessingHistory.FPS = workItem.FPS;
                    entry.ProcessingHistory.Percentage = workItem.Percent;
                    entry.ProcessingHistory.Speed = workItem.Speed;
                    entry.ProcessingHistory.SSIM = workItem.SSIM;

                    historyEntries.Update(entry);
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

        public async Task<SortedSet<MediaHistory>> GetHistory()
        {
            using var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo));

            return await Task.Run(() =>
            {
                try
                {
                    var histories = db.GetCollection<MediaHistory>();
                    return new SortedSet<MediaHistory>(histories.Include(x => x.Entries).FindAll());
                }
                catch (InvalidCastException)
                {
                    db.DropCollection(HISTORYTABLE);
                    return new();
                }
            });
        }

        public async Task<SortedSet<HistoryEntry>> GetProcessHistoryAsync(string filePath)
        {
            using var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo));

            return await Task.Run(() =>
            {
                try
                {
                    var histories = db.GetCollection<MediaHistory>();
                    var history = histories.Query().Include(x => x.Entries).Where(x => x.FilePath == filePath).FirstOrDefault();
                    return new SortedSet<HistoryEntry>(history?.Entries ?? new());
                }
                catch (InvalidCastException)
                {
                    db.DropCollection(HISTORYTABLE);
                    return new();
                }
            });
        }

        public Guid StartProcessing(WorkItem wi)
        {
            using var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo));

            var history = new MediaHistory();
            ILiteCollection<MediaHistory> histories = default;

            try
            {
                histories = db.GetCollection<MediaHistory>();
                history = histories.Query().Include(x => x.Entries).Where(x => x.FilePath == wi.SourceFile).FirstOrDefault();
            }
            catch (InvalidCastException)
            {
                db.DropCollection(HISTORYTABLE);
                histories = db.GetCollection<MediaHistory>();
                //todo: report on screen
            }

            if (history == default)
            {
                history = new MediaHistory()
                {
                    FilePath = wi.SourceFile
                };
                histories.Insert(history);
            }

            history.Entries ??= new();

            var processingHistory = new ProcessingHistory()
            {
                Arguments = wi.Arguments.ToList(),
                DestinationFilePath = wi.DestinationFile,
                FilterID = wi.Job.FilterID,
                Preset = wi.Job.PresetName
            };

            var entry = new HistoryEntry()
            {
                HistoryID = Guid.NewGuid(),
                Started = DateTime.Now,
                Type = "Processed",
                ProcessingHistory = processingHistory
            };

            history.Entries.Add(entry);

            histories.EnsureIndex(x => x.Id);
            histories.EnsureIndex(x => x.Entries);

            histories.Update(history);

            return entry.HistoryID;
        }
    }
}
