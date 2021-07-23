using Compressarr.Application;
using Compressarr.Pages.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Shared
{
    public partial class MainLayout
    {
        [Inject] IApplicationInitialiser ApplicationInitialiser { get; set; }
        [Inject] IApplicationService ApplicationService { get; set; }
        [Inject] ISnackbar Snackbar { get; set; }

        protected LayoutEngineBase layoutEngine = new();

        bool IsReady => (ApplicationService.InitialiseFFmpeg?.IsCompleted ?? false) && (ApplicationService.InitialisePresets?.IsCompleted ?? false);

        protected void LayoutEngineStateChanged()
        {
            InvokeAsync(StateHasChanged);
        }

        bool _drawerOpen = false;

        void DrawerToggle()
        {
            _drawerOpen = !_drawerOpen;
        }

        protected override void OnInitialized()
        {
            ApplicationService.OnBroadcast += (s, message) =>
            {
                InvokeAsync(() =>
                {
                    Snackbar.Add(message);
                    InvokeAsync(StateHasChanged);
                });
            };

            ApplicationInitialiser.OnComplete += (s, args) => InvokeAsync(StateHasChanged);

            base.OnInitialized();
        }

        MudTheme MyCustomTheme = new MudTheme()
        {
            Palette = new Palette()
            {
                Primary = Colors.DeepPurple.Default,
                Secondary = "#89b73a",
                Tertiary = "#b7513a",
                AppbarText = "#fff",
                AppbarBackground = Colors.BlueGrey.Default
            },

            LayoutProperties = new LayoutProperties()
            {
                DrawerWidthLeft = "260px",
                DrawerWidthRight = "300px"
            },
            Typography = new Typography()
            {
                H1 = new() { FontSize = "4rem", FontFamily = new string[] { "Roboto", "Helvetica", "Arial", "sans-serif" }, FontWeight = 300, LineHeight = 1.5 }
            }
        };
    }
}
