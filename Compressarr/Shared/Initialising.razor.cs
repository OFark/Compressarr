using Compressarr.Application;
using Microsoft.AspNetCore.Components;

namespace Compressarr.Shared
{
    public partial class Initialising
    {
        [Inject] IApplicationInitialiser ApplicationInitialiser { get; set; }
        [Inject] IApplicationService ApplicationService { get; set; }

        protected override void OnInitialized()
        {
            ApplicationInitialiser.OnUpdate += FFmpegInitialiserService_OnUpdate;
            base.OnInitialized();
        }

        private void FFmpegInitialiserService_OnUpdate(object sender, JobProcessing.Models.Update e)
        {
            InvokeAsync(StateHasChanged);
        }
    }
}
