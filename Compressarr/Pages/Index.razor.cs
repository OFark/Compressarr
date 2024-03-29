﻿using Compressarr.Presets;
using Compressarr.Filtering;
using Compressarr.JobProcessing;
using Compressarr.JobProcessing.Models;
using Compressarr.Pages.Services;
using Compressarr.Services;
using Compressarr.Services.Base;
using Compressarr.Application;
using Microsoft.AspNetCore.Components;
using System;
using System.Threading.Tasks;

namespace Compressarr.Pages
{
    public partial class Index
    {
        [Inject]
        IPresetManager PresetManager { get; set; }
        [Inject]
        IFilterManager FilterManager { get; set; }
        [Inject]
        IJobManager JobManager { get; set; }
        [Inject]
        ILayoutService LayoutService { get; set; }
        [Inject]
        IRadarrService RadarrService { get; set; }
        [Inject]
        ISonarrService SonarrService { get; set; }
        [Inject]
        IApplicationService ApplicationService { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "This isn't readonly, it's modified in a razor component")]
        private Job newJob = new();

        private StatusResult filterStatus = new();
        private StatusResult presetStatus = new();
        private StatusResult radarrStatus = new();
        private StatusResult sonarrStatus = new();

        private bool AllGood => filterStatus.Status == ServiceStatus.Ready &&
                                presetStatus.Status == ServiceStatus.Ready &&
                                (radarrStatus.Status == ServiceStatus.Ready ||
                                sonarrStatus.Status == ServiceStatus.Ready);


        protected async override Task OnInitializedAsync()
        {
            LayoutService.OnStateChanged += LayoutService_OnStateChanged;

            await ApplicationService.InitialiseFFmpeg;

            radarrStatus = await RadarrService.GetStatus();
            sonarrStatus = await SonarrService.GetStatus();
            filterStatus = await FilterManager.GetStatus();
            presetStatus = await PresetManager.GetStatus();

            await base.OnInitializedAsync();
        }

        private async void LayoutService_OnStateChanged(object sender, EventArgs e)
        {
            await InvokeAsync(StateHasChanged);
        }
    }
}
