using Compressarr.Application;
using Compressarr.JobProcessing;
using Compressarr.Pages.Services;
using Compressarr.Presets;
using Compressarr.Presets.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Shared
{
    public partial class PresetView
    {

        [Parameter]
        public bool NewPreset { get; set; }

        [Parameter]
        public EventCallback OnAdd { get; set; }

        [Parameter]
        public FFmpegPreset Preset { get; set; }

        [Inject] IApplicationService ApplicationService { get; set; }
        [Inject] IDialogService DialogService { get; set; }
        private bool DisabledNoVideo => Preset.VideoEncoder?.IsCopy ?? true;
        private bool DisabledVideoBitrate => Preset.VideoBitRate.HasValue || Preset.VideoBitRateAutoCalc;
        [Inject] IJobManager JobManager { get; set; }
        [Inject] ILayoutService LayoutService { get; set; }
        [Inject] IPresetManager PresetManager { get; set; }
        
        
        protected override void OnInitialized()
        {
            LayoutService.OnStateChanged += (o, e) => InvokeAsync(StateHasChanged);
            base.OnInitialized();
        }

        private void AudioPresetUpdate()
        {
            var firstCoverAny = Preset.AudioStreamPresets.FirstOrDefault(f => f.CoversAny);

            if (firstCoverAny == null)
            {
                Preset.AudioStreamPresets.Add(new());
            }
            else
            {
                if (firstCoverAny != Preset.AudioStreamPresets.Last())
                {
                    var indexofRemoval = Preset.AudioStreamPresets.IndexOf(firstCoverAny) + 1;
                    Preset.AudioStreamPresets.RemoveRange(indexofRemoval, Preset.AudioStreamPresets.Count - indexofRemoval);
                }
            }
            InvokeAsync(StateHasChanged);
        }

        private void CopyAudioStreamPreset(FFmpegAudioStreamPreset asPreset)
        {
            var index = Preset.AudioStreamPresets.IndexOf(asPreset);

            if (index >= 0)
            {
                var clone = asPreset.Clone();
                Preset.AudioStreamPresets.Insert(index, clone);
                InvokeAsync(StateHasChanged);
            }
        }

        private async void DeletePreset()
        {
            using (LayoutService.Working("Deleting..."))
            {

                bool? result = await DialogService.ShowMessageBox(
                "Warning",
                "Deleting can not be undone!",
                yesText: "Delete!", cancelText: "Cancel");

                if (result ?? false)
                {
                    await PresetManager.DeletePresetAsync(Preset);
                    LayoutService.RaiseChange();
                }
                await InvokeAsync(StateHasChanged);
            }
        }

        private async void SavePreset()
        {
            using (LayoutService.Working("Saving..."))
            {

                await PresetManager.AddPresetAsync(Preset);

                JobManager.InitialiseJobs(Preset, ApplicationService.AppStoppingCancellationToken);

                if (OnAdd.HasDelegate)
                {
                    await OnAdd.InvokeAsync();
                }

                LayoutService.RaiseChange();
                await InvokeAsync(StateHasChanged);
            }
        }
    }
}
