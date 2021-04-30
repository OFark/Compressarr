using Compressarr.FFmpegFactory;
using Compressarr.Filtering;
using Compressarr.JobProcessing;
using Compressarr.JobProcessing.Models;
using Compressarr.Pages.Services;
using Compressarr.Settings;
using Microsoft.AspNetCore.Components;
using System;

namespace Compressarr.Pages
{
    public partial class Index
    {
        [Inject]
        IFFmpegManager FFmpegManager { get; set; }
        [Inject]
        IFilterManager FilterManager { get; set; }
        [Inject]
        IJobManager JobManager { get; set; }
        [Inject]
        ILayoutService LayoutService { get; set; }
        [Inject]
        ISettingsManager settingsManager { get; set; }
        [Inject]
        NavigationManager NavManager { get; set; }

        private Compressarr.JobProcessing.Models.Job newJob = new Job();

        protected override void OnInitialized()
        {
            if (settingsManager.RadarrSettings == null)
            {
                NavManager.NavigateTo("options");
            }

            LayoutService.OnStateChanged += LayoutService_OnStateChanged;

            base.OnInitialized();
        }

        private async void LayoutService_OnStateChanged(object sender, EventArgs e)
        {
            await InvokeAsync(StateHasChanged);
        }

        private void addJob()
        {
            newJob = new();
        }
    }
}
