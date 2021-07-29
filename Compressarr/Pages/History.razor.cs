using Compressarr.History;
using Compressarr.History.Models;
using Compressarr.Shared.Models;
using Microsoft.AspNetCore.Components;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Compressarr.Pages
{
    public partial class History
    {
        [Inject] IHistoryService HistoryService { get; set; }
        
        private SortedSet<MediaHistory> JobHistory { get; set; }

        protected override async Task OnInitializedAsync()
        {
            JobHistory = await HistoryService.GetHistory();

            await base.OnInitializedAsync();
        }
    }
}
