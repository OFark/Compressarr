using Compressarr.Application;
using Compressarr.FFmpeg;
using Compressarr.Filtering;
using Compressarr.Helpers;
using Compressarr.JobProcessing;
using Compressarr.JobProcessing.Models;
using Compressarr.Pages.Services;
using Compressarr.Presets;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Compressarr.Shared
{
    public partial class JobView : IDisposable
    {
        private bool canReload, editJob, mouseOnButton;
        private CancellationTokenSource cts = new();
        private bool DisableCancelMediaInfoButton = true;
        private bool isExpanded;
        private bool disposedValue;
        MudForm jobEditorForm;
        private double initialisationProgress;
        [Parameter]
        public Job Job { get; set; }

        [Parameter]
        public bool NewJob { get; set; }

        [Parameter]
        public EventCallback OnAdd { get; set; }

        [Inject] IApplicationService ApplicationService { get; set; }
        private Color ButtonColour =>
            Job.Condition.SafeToRun ? Color.Primary : Job.Condition.SafeToInitialise ? Color.Secondary : Job.State == JobState.Error ? Color.Error : Color.Warning;

        private string ButtonText => (mouseOnButton ? Job.Condition.SafeToRun ? "Go!" : Job.Condition.SafeToInitialise ? "Initialise" : Job.Condition.CanCancel ? "Cancel" : null : null) ?? Job.State.ToString().ToCamelCaseSplit();
        [Inject] IDialogService DialogService { get; set; }
        private bool Editing => editJob || NewJob;
        [Inject] IFFmpegProcessor FFmpegProcessor { get; set; }
        private string FilterImageSrc => $"https://raw.githubusercontent.com/{FilterType.Capitalise()}/{FilterType.Capitalise()}/develop/Logo/{FilterType.Capitalise()}.svg";
        [Inject] IFilterManager FilterManager { get; set; }
        private string FilterType => Job?.MediaSource.ToString().ToLower();
        [Inject] IHistoryService HistoryService { get; set; }
        [Inject] IJobManager JobManager { get; set; }
        [Inject] ILayoutService LayoutService { get; set; }

        private bool showFolderSource; 
        public string JobFilterID
        {
            get
            {
                return Job.FilterID != default ? Job.FilterID.ToString() : showFolderSource ? "folder" : default;
            }
            set
            {
                if(Guid.TryParse(value, out var id))
                {
                    Job.FilterID = id;
                    showFolderSource = false;
                    Job.SourceFolder = default;
                }
                else
                {
                    if(value == "folder")
                    {
                        Job.FilterID = default;
                        Job.Filter = null;
                        showFolderSource = true;
                    }
                }
            }
        }
        private int? MaxComp
        {
            get
            {
                return Job.MaxCompression.HasValue ? (int)(Job.MaxCompression * 100) : null;
            }
            set
            {
                if (value.HasValue)
                {
                    Job.MaxCompression = (decimal)value / 100;
                }
                else
                {
                    Job.MaxCompression = null;
                }
            }
        }
        private decimal? MaxCompPost
        {
            get
            {
                return Job.ArgumentCalculationSettings.AutoCalculationCompPost.HasValue ? Job.ArgumentCalculationSettings.AutoCalculationCompPost * 100 : null;
            }
            set
            {
                if (value.HasValue)
                {
                    Job.ArgumentCalculationSettings.AutoCalculationCompPost = value / 100;
                }
                else
                {
                    Job.ArgumentCalculationSettings.AutoCalculationCompPost = null;
                }
            }
        }

        private int? MinSSIM
        {
            get
            {
                return Job.MinSSIM.HasValue ? (int)(Job.MinSSIM * 100) : null;
            }
            set
            {
                if (value.HasValue)
                {
                    Job.MinSSIM = (decimal)value / 100;
                }
                else
                {
                    Job.MinSSIM = null;
                }
            }
        }
        private decimal? MinSSIMPost
        {
            get
            {
                return Job.ArgumentCalculationSettings.AutoCalculationSSIMPost.HasValue ? Job.ArgumentCalculationSettings.AutoCalculationSSIMPost * 100 : null;
            }
            set
            {
                if (value.HasValue)
                {
                    Job.ArgumentCalculationSettings.AutoCalculationSSIMPost = value / 100;
                }
                else
                {
                    Job.ArgumentCalculationSettings.AutoCalculationSSIMPost = null;
                }
            }
        }

        [Inject] IPresetManager PresetManager { get; set; }

        private bool SaveEnabled => (Editing || NewJob) && Job?.FilterID != null && Job?.Preset != null && Job?.DestinationFolder != null;

        private decimal? VideoBitRateTarget
        {
            get
            {
                return Job.ArgumentCalculationSettings.VideoBitRateTarget.HasValue ? (Job.ArgumentCalculationSettings.VideoBitRateTarget * 100) : null;
            }
            set
            {
                if (value.HasValue)
                {
                    Job.ArgumentCalculationSettings.VideoBitRateTarget = value / 100;
                }
                else
                {
                    Job.ArgumentCalculationSettings.VideoBitRateTarget = null;
                }
            }
        }
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (Job != null)
                    {
                        Job.StatusUpdate -= JobStatusUpdate;
                    }
                    cts.Cancel();
                    cts.Dispose();
                }
                disposedValue = true;
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

        protected override void OnInitialized()
        {
            if (NewJob) Job.ArgumentCalculationSettings ??= new();
            Job.StatusUpdate += JobStatusUpdate;
            Job.InitialisationProgress = new Progress<double>(JobProgress);
        }
        private void CancelProcessing(WorkItem wi)
        {
            if (wi.CancellationToken.CanBeCanceled)
            {
                wi.CancellationTokenSource.Cancel();
            }
        }

        private async Task ClearAutoCalcHistory(WorkItem wi)
        {
            await HistoryService.ClearAutoCalcResult(wi.Media.FFProbeMediaInfo.GetStableHash());
        }

        private async void DeleteJob()
        {
            using (LayoutService.Working("Deleting..."))
            {
                bool? result = await DialogService.ShowMessageBox(
                "Warning",
                "Deleting can not be undone!",
                yesText: "Delete!", cancelText: "Cancel");

                if (result ?? false)
                {
                    await JobManager.DeleteJob(Job);
                    await InvokeAsync(StateHasChanged);
                }
            }
        }
        private async Task GetMediaInfo(WorkItem wi)
        {
            wi.CancellationTokenSource = new();
            DisableCancelMediaInfoButton = false;

            try
            {
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
            finally
            {
                DisableCancelMediaInfoButton = true;
            }
        }

        private void JobProgress(double val)
        {
            try
            {

                initialisationProgress = val;
                InvokeAsync(StateHasChanged);
            }
            catch (ObjectDisposedException) { }
        }
        private void JobStatusUpdate(object caller, EventArgs args)
        {
            InvokeAsync(StateHasChanged);
        }

        private async Task PrepareWorkItem(WorkItem wi)
        {
            wi.CancellationTokenSource = new();
            await JobManager.PrepareWorkItem(wi, Job.Preset, wi.CancellationToken);
        }

        private async Task ProcessWorkItem(WorkItem wi)
        {
            wi.CancellationTokenSource = new();
            await JobManager.ProcessWorkItem(wi, wi.CancellationToken);
        }

        private async void ReInitialise()
        {
            await JobManager.InitialiseJob(Job, ApplicationService.AppStoppingCancellationToken);
        }

        private void ReloadJob()
        {
            using (LayoutService.Working("Reloading..."))
            {
                Job = JobManager.ReloadJob(Job, ApplicationService.AppStoppingCancellationToken);
                canReload = false;
                InvokeAsync(StateHasChanged);
            }
        }

        private async void SaveJob()
        {
            jobEditorForm.Validate();
            if (jobEditorForm.IsValid)
            {
                var success = await JobManager.AddJobAsync(Job, ApplicationService.AppStoppingCancellationToken);
                editJob = !success;
                canReload = !success && !NewJob;

                if(success && NewJob)
                {
                    Job = new();
                    Job.ArgumentCalculationSettings = new();
                    isExpanded = false;
                }

                LayoutService.RaiseChange();
            }

        }

        private void StartJob()
        {
            if (Job.Condition.SafeToRun)
            {
                JobManager.RunJob(Job);
            }
            else if (Job.Condition.SafeToInitialise)
            {
                JobManager.InitialiseJob(Job, ApplicationService.AppStoppingCancellationToken);
            }
            else if (Job.Condition.CanCancel)
            {
                JobManager.CancelJob(Job);
            }
        }

        private async Task ToggleDetails(WorkItem wi)
        {
            wi.ShowDetails = !wi.ShowDetails;

            //if (wi.ShowDetails)
            //{
            //    cancellationTokenSource = new();

            //    if (wi.Arguments == null && !wi.Condition.Prepare.Processing)
            //    {
            //        await ArgumentService.SetArguments(Job.Preset, wi, cancellationTokenSource.Token);
            //    }
            //}
            //else
            //{
            //    if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
            //    {
            //        cancellationTokenSource.Cancel();
            //    }
            //}

            await InvokeAsync(StateHasChanged);
        }
    }
}
