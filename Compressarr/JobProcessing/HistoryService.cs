using Compressarr.Application;
using Compressarr.JobProcessing.Models;
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
        private readonly IFileService fileService;
        private ILogger<HistoryService> logger;

        private const string HISTORYTABLE = "History";

        public HistoryService(IFileService fileService, ILogger<HistoryService> logger)
        {
            this.fileService = fileService;

            this.logger = logger;
        }

        public Guid StartProcessing(string filePath, string filter, string preset, IEnumerable<string> arguments)
        {
            using (var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo)))
            {
                var histories = db.GetCollection<History>(HISTORYTABLE);
                var history = histories.Query().Include(x => x.Entries).Where(x => x.FilePath == filePath).FirstOrDefault();
                
                if(history == default)
                {
                    history = new History()
                    {
                        FilePath = filePath
                    };
                    histories.Insert(history);
                }

                history.Entries ??= new();

                var historyProcessing = new HistoryProcessing()
                {
                    Type = "Processed",
                    Arguments = arguments.ToList(),
                    Filter = filter,
                    Preset = preset,
                    Started = DateTime.Now,
                    HistoryID = Guid.NewGuid()
                };

                history.Entries.Add(historyProcessing);

                histories.EnsureIndex(x => x.FilePath);
                histories.EnsureIndex(x => x.Entries);

                histories.Update(history);

                return historyProcessing.HistoryID;
            }
        }

        public void EndProcessing(Guid historyID, bool succeeded, WorkItem workItem)
        {
            using (var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo)))
            {
                var histories = db.GetCollection<History>(HISTORYTABLE);
                var history = histories.Query().Include(x => x.Entries).Where(x => x.FilePath == workItem.SourceFile).FirstOrDefault();

                var historyProcessing = (HistoryProcessing)history.Entries.FirstOrDefault(x => x.HistoryID == historyID);

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

        public SortedSet<IHistoryEntry> GetHistory(string filePath)
        {
            using (var db = new LiteDatabase(fileService.GetAppFilePath(AppFile.mediaInfo)))
            {
                var histories = db.GetCollection<History>(HISTORYTABLE);
                var history = histories.Query().Where(x => x.FilePath == filePath).FirstOrDefault();

                return  new SortedSet<IHistoryEntry>(history?.Entries ?? new());

            }

        }
        
    }
}
