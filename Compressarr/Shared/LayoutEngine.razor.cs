using Compressarr.Pages.Services;
using Microsoft.AspNetCore.Components;
using System;
using System.Threading.Tasks;

namespace Compressarr.Shared
{
    public class LayoutEngineBase : ComponentBase
    {
        [Inject]
        public ILayoutService LayoutService { get; set; }

        /// <summary>
        /// This is used for the contained Razor markup.
        /// </summary>
        [Parameter]
        public RenderFragment ChildContent { get; set; }

        [Parameter]
        public EventCallback OnStateChanged { get; set; }

        protected override void OnInitialized()
        {
            LayoutService.OnStateChanged += LayoutService_OnStateChanged;
        }

        private async void LayoutService_OnStateChanged(object sender, EventArgs e)
        {
            await InvokeAsync(async () => await OnStateChanged.InvokeAsync());
        }
        
        protected void ConfirmYes()
        {
            //Do not clear LayoutService.AdditionalLogon here, were in the middle of an await.
            LayoutService.ConfirmDialog(true);

        }

        protected void ConfirmNo()
        {
            LayoutService.ConfirmDialog(false);
        }
    }
}
