using Compressarr.Application;
using Compressarr.JobProcessing.Models;
using LiteDB;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Compressarr.JobProcessing
{
    public class HistoryService : IHistoryService
    {
        private readonly IFileService fileService;
        private readonly ILogger<HistoryService> logger;

        private const string HISTORYTABLE = "History";

        public HistoryService(IFileService fileService, ILogger<HistoryService> logger)
        {
            this.fileService = fileService;

            this.logger = logger;
        }

        public Guid StartProcessing(int mediaID, string filePath, string filter, string preset, IEnumerable<string> arguments)
        {
            using var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo));

            var histories = db.GetCollection<History>();
            var history = histories.Query().Include(x => x.Entries).Where(x => x.MediaID == mediaID).FirstOrDefault();

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
                Filter = filter,
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

        public void EndProcessing(Guid historyEntryID, bool succeeded, WorkItem workItem)
        {
            using var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo));

            var histories = db.GetCollection<History>(HISTORYTABLE);
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

        public SortedSet<HistoryProcessing> GetProcessHistory(int mediaID)
        {
            using var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo));

            var histories = db.GetCollection<History>(HISTORYTABLE);
            var history = histories.Query().Where(x => x.MediaID == mediaID).FirstOrDefault();

            return new SortedSet<HistoryProcessing>(history?.Entries ?? new());

        }
        
    }
}
