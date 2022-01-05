using Compressarr.FFmpeg;
using Compressarr.History;
using Compressarr.JobProcessing;
using Compressarr.JobProcessing.Models;
using Microsoft.AspNetCore.Components;
using System.Threading.Tasks;

namespace Compressarr.Shared
{
    public partial class JobViewCurrentJob
    {
        [Inject] IFFmpegProcessor FFmpegProcessor { get; set; }
        [Inject] IHistoryService HistoryService { get; set; }
        [Inject] IJobManager JobManager { get; set; }

        [Parameter]
        public WorkItem CurrentWorkItem { get; set; }

        private async Task ClearAutoCalcHistory(WorkItem wi)
        {
            if (wi.Media.FFProbeMediaInfo == null)
            {
                await GetMediaInfo(wi);
            }

            if (wi.Media.FFProbeMediaInfo != null)
            {
                await HistoryService.ClearAutoCalcResult(wi.Media.FFProbeMediaInfo.GetStableHash());
            }
        }

        private async Task GetMediaInfo(WorkItem wi)
        {
            wi.CancellationTokenSource = new();

            var ffProbeResponse = await FFmpegProcessor.GetFFProbeInfo(wi.Media.FilePath, wi.CancellationToken);
            if (ffProbeResponse.Success)
            {
                wi.Media.FFProbeMediaInfo = ffProbeResponse.Result;
            }
            else
            {
                wi.Update(Update.Error(ffProbeResponse.ErrorMessage));
            }
        }

        protected async Task ImportVideo(WorkItem wi)
        {
            var importReport = await JobManager.ImportVideo(wi, wi.Job.MediaSource);
            if (importReport != null)
            {
                wi.Update(Update.Warning(importReport));
            }
        }

        private static void CancelProcessing(WorkItem wi)
        {
            if (wi.CancellationToken.CanBeCanceled)
            {
                wi.CancellationTokenSource.Cancel();
            }
        }
    }
}
