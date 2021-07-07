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
        private CancellationTokenSource cancellationTokenSource;
        private bool canReload, editJob, mouseOnButton;
        private double initialisationProgress;
        [Parameter]
        public Job Job { get; set; }

        [Parameter]
        public bool NewJob { get; set; }

        [Parameter]
        public EventCallback OnAdd { get; set; }

        [Inject] IHistoryService HistoryService { get; set; }

        [Inject] IApplicationService ApplicationService { get; set; }
        [Inject] IArgumentService ArgumentService { get; set; }
        private Color ButtonColour =>
            Job.Condition.SafeToRun ? Color.Primary : Job.Condition.SafeToInitialise ? Color.Secondary : Job.State == JobState.Error ? Color.Error : Color.Warning;

        private string ButtonText => (mouseOnButton ? Job.Condition.SafeToRun ? "Go!" : Job.Condition.SafeToInitialise ? "Initialise" : Job.Condition.CanCancel ? "Cancel" : null : null) ?? Job.State.ToString().ToCamelCaseSplit();
        [Inject] IDialogService DialogService { get; set; }
        private bool Editing => editJob || NewJob;
        private string FilterImageSrc => $"https://raw.githubusercontent.com/{FilterType.Capitalise()}/{FilterType.Capitalise()}/develop/Logo/{FilterType.Capitalise()}.svg";
        [Inject] IFilterManager FilterManager { get; set; }
        private string FilterType => Job?.Filter?.MediaSource.ToString().ToLower();
        [Inject] IJobManager JobManager { get; set; }
        [Inject] ILayoutService LayoutService { get; set; }
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

        [Inject] IMediaInfoService MediaInfoService { get; set; }
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

        private int? MinSSIMPost
        {
            get
            {
                return Job.ArgumentCalculationSettings.AutoCalculationPost.HasValue ? (int)(Job.ArgumentCalculationSettings.AutoCalculationPost * 100) : null;
            }
            set
            {
                if (value.HasValue)
                {
                    Job.ArgumentCalculationSettings.AutoCalculationPost = (decimal)value / 100;
                }
                else
                {
                    Job.ArgumentCalculationSettings.AutoCalculationPost = null;
                }
            }
        }

        [Inject] IPresetManager PresetManager { get; set; }

        private bool SaveEnabled => (Editing || NewJob) && Job?.FilterID != null && Job?.PresetName != null && Job?.DestinationFolder != null;

        void IDisposable.Dispose()
        {
            if (Job != null)
            {
                GC.SuppressFinalize(this);
                Job.StatusUpdate -= JobStatusUpdate;
            }
        }

        protected override void OnInitialized()
        {
            if (NewJob) Job.ArgumentCalculationSettings ??= new();
            Job.StatusUpdate += JobStatusUpdate;
            Job.InitialisationProgress = new Progress<double>(JobProgress);
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

        private async Task ClearAutoCalcHistory(WorkItem wi)
        {
            await HistoryService.ClearAutoCalcResult(wi.Media.FFProbeMediaInfo.GetStableHash());
        }

        private bool DisableCancelMediaInfoButton = true;

        private async Task GetMediaInfo(WorkItem wi)
        {
            wi.CancellationTokenSource = new();
            DisableCancelMediaInfoButton = false;

            try
            {
                var ffProbeResponse = await MediaInfoService.GetMediaInfo(wi.Media, wi.CancellationToken);
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

        private async Task PrepareWorkItem(WorkItem wi)
        {
            wi.CancellationTokenSource = new();
            await JobManager.PrepareWorkItem(wi, Job.Preset, wi.CancellationToken);
        }

        private void CancelProcessing(WorkItem wi)
        {
            if (wi.CancellationToken.CanBeCanceled)
            {
                wi.CancellationTokenSource.Cancel();
            }
        }

        private void JobProgress(double val)
        {
            try
            {

                initialisationProgress = val;
                InvokeAsync(StateHasChanged);
            }
            catch (ObjectDisposedException _) { }
        }

        private void JobStatusUpdate(object caller, EventArgs args)
        {
            InvokeAsync(StateHasChanged);
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
            using (LayoutService.Working("Saving..."))
            {

                var success = await JobManager.AddJobAsync(Job, ApplicationService.AppStoppingCancellationToken);
                editJob = !success;
                canReload = !success && !NewJob;

                if (success && NewJob)
                {
                    if (OnAdd.HasDelegate)
                    {
                        _ = OnAdd.InvokeAsync();
                        return;
                    }
                }
                else
                {
                    await InvokeAsync(StateHasChanged);
                }
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
