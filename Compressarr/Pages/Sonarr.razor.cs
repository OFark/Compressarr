using Compressarr.Application;
using Compressarr.Filtering;
using Compressarr.Filtering.Models;
using Compressarr.Helpers;
using Compressarr.JobProcessing;
using Compressarr.Pages.Services;
using Compressarr.Services;
using Compressarr.Services.Base;
using Compressarr.Services.Models;
using Compressarr.Shared.Models;
using Humanizer;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Pages
{
    public partial class Sonarr
    {

        private string AlertMessage;
        private Guid filterID;
        private string filterPropertyStr = "Title";
        private string newFilterTextButton = "Add";
        private IEnumerable<Series> Series = new HashSet<Series>();
        private HashSet<TreeItemData> SeriesTreeItems = new();
        private bool showSaveDialog = false;
        [Inject] IApplicationService ApplicationService { get; set; }
        [Inject] IDialogService DialogService { get; set; }
        private List<DynamicLinqFilter> DlFilters { get; set; } = new();
        private string Filter { get; set; } = "";
        private FilterComparitor FilterComparitor => FilterManager.GetComparitors(FilterProperty).FirstOrDefault(x => x.Value == FilterComparitorStr);
        private string FilterComparitorStr { get; set; } = "==";
        private Guid FilterID
        {
            get
            {
                return filterID;
            }
            set
            {
                filterID = value;
                if (filterID != default)
                {

                    var filter = FilterManager.GetFilter(filterID);
                    if (filter != null)
                    {
                        FilterName = filter.Name;
                        DlFilters = filter.Filters.ToList().JsonClone();
                        showSaveDialog = true;
                        _ = FilterUpdate();
                    }
                    else
                    {
                        ClearFilters();
                    }
                }
            }
        }

        private decimal FilterIntValue
        {
            get
            {
                return decimal.TryParse(FilterValue, out var x) ? x : 0;
            }
            set
            {
                FilterValue = value.ToString();
            }
        }

        [Inject] IFilterManager FilterManager { get; set; }
        private string FilterName { get; set; }
        private FilterProperty FilterProperty => FilterManager.SonarrFilterProperties.FirstOrDefault(x => x.Value == FilterPropertyStr);
        private string FilterPropertyStr { get => filterPropertyStr; set { FilterValues = new HashSet<string>(); filterPropertyStr = value; } }
        private string FilterValue { get; set; }
        private IEnumerable<string> FilterValues { get; set; }
        [Inject] IJobManager JobManager { get; set; }
        [Inject] ILayoutService LayoutService { get; set; }
        [Inject] ISonarrService SonarrService { get; set; }
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await LoadSeries();
            }
        }

        private async void AddFilter()
        {

            if (FilterProperty != null && FilterComparitor != null && !string.IsNullOrWhiteSpace(FilterValue))
            {
                var dlFilter = GetCurrentFilter();


                if (DlFilters.Count == 0)
                {
                    dlFilter.IsFirst = true;
                }
                else
                {
                    dlFilter.LogicalOperator = "and";
                }

                DlFilters.Add(dlFilter);
                ResetFilterInputs();

                await FilterUpdate();
            }

        }

        private async Task BuildSeriesTreeView()
        {
            SeriesTreeItems = new();
            await InvokeAsync(StateHasChanged);
            await Task.Delay(500); //this is to force the screen to redraw the Treeview to clear it's cache

            var series = Series.GroupBy(x => x.Path)
                .Select(x => new
                {
                    x.First().Added,
                    x.First().EpisodeFileCount,
                    x.First().Genres,
                    x.First().Id,
                    x.First().Path,
                    x.First().SizeOnDisk,
                    x.First().Title,
                    Seasons = x.GroupBy(s => s.Season.SeasonNumber)
                        .Select(y => new
                        {
                            SeasonNumber = y.Key,
                            y.First().Season.Statistics,
                            EpisodeFiles = y.Select(g => g.Season.EpisodeFile)
                        })
                }
            );

            foreach (var ser in series)
            {
                var tid = new TreeItemData(ser.Id, ser.Title, null, null, new());

                tid.TreeItems.Add(new TreeItemData($"Added", ser.Added.ToShortDateString()));
                tid.TreeItems.Add(new TreeItemData($"Episode files", ser.EpisodeFileCount));
                tid.TreeItems.Add(new TreeItemData($"Size on disk", ser.SizeOnDisk.Bytes().Humanize("0.00")));
                tid.TreeItems.Add(new TreeItemData($"Path", ser.Path));

                tid.TreeItems.Add(new TreeItemData("Genres", null, new(ser.Genres.Select(g => new TreeItemData(g)))));

                tid.TreeItems.Add(
                    new TreeItemData(
                        "Seasons", null, new(
                            ser.Seasons.Select(s =>
                                new TreeItemData($"Season {s.SeasonNumber}", null, new(s.EpisodeFiles.Select(x => x.MediaInfo.VideoCodec).Distinct()), new()
                                {
                                    new($"Size on disk", s.Statistics?.SizeOnDisk.Bytes().Humanize("0.00") ?? "Unknown"),
                                    new($"Episodes", s.Statistics?.EpisodeFileCount ?? -1,
                                        s.EpisodeFiles?.Select(x => new TreeItemData(x.RelativePath, x.Size.Bytes().Humanize("0.00"), new HashSet<string>() { x.MediaInfo.VideoCodec, x.MediaInfo.AudioCodec, $"{x.MediaInfo.AudioChannels}" })).ToHashSet()
                                    ),
                                })
                            )
                        )
                    )
                );

                tid.Badges = new(ser.Seasons
                    .SelectMany(x => x.EpisodeFiles?.Select(e => e.MediaInfo.VideoCodec))
                    .Distinct());

                SeriesTreeItems.Add(tid);
            }

            await InvokeAsync(StateHasChanged);
        }

        public async Task<HashSet<TreeItemData>> LoadServerData(TreeItemData parentNode)
        {
            return parentNode?.TreeItems;
        }

        private async void ClearFilters()
        {
            DlFilters = new List<DynamicLinqFilter>();
            FilterName = null;
            FilterID = default;
            await FilterUpdate();
        }

        private async void DeleteFilter(DynamicLinqFilter filter)
        {
            if (DlFilters.Contains(filter))
            {
                DlFilters.Remove(filter);
                var firstFilter = DlFilters.FirstOrDefault();
                if (firstFilter != null)
                {
                    firstFilter.LogicalOperator = null;
                }
            }
            else
            {
                foreach (var dlFilter in DlFilters)
                {
                    if (dlFilter.SubFilters.Contains(filter))
                    {
                        dlFilter.SubFilters.Remove(filter);
                        if (dlFilter.SubFilters.Count == 1)
                        {
                            dlFilter.SubFilters.First().LogicalOperator = dlFilter.LogicalOperator;
                            DlFilters.Insert(DlFilters.IndexOf(dlFilter), dlFilter.SubFilters.First());
                            DlFilters.Remove(dlFilter);
                        }
                        break;
                    }
                }
            }

            await FilterUpdate();
        }

        private async void DeleteSavedFilter()
        {
            bool? result = await DialogService.ShowMessageBox(
                "Warning",
                "Deleting can not be undone!",
                yesText: "Delete!", cancelText: "Cancel");

            if (result ?? false)
            {
                if (!string.IsNullOrWhiteSpace(FilterName))
                {
                    await FilterManager.DeleteFilter(FilterName);
                    FilterName = null;
                    showSaveDialog = false;
                    ClearFilters();
                }
            }
        }

        private async Task FilterUpdate()
        {
            List<string> filterArguments;

            if (DlFilters.Any())
            {
                Filter = FilterManager.ConstructFilterQuery(DlFilters, out filterArguments);
                newFilterTextButton = "And";
            }
            else
            {
                Filter = string.Empty;
                filterArguments = new();
                newFilterTextButton = "Add";
            }


            SonarrService.SeriesFilter = Filter;
            SonarrService.SeriesFilterValues = filterArguments;

            await RefreshSeries(this, null);

            StateHasChanged();
        }

        private DynamicLinqFilter GetCurrentFilter() => new(FilterProperty, FilterComparitor, FilterValue, FilterValues);

        private async void GroupFilter(DynamicLinqFilter filter)
        {

            if (FilterProperty != null && FilterComparitor != null && !string.IsNullOrWhiteSpace(FilterValue))
            {
                var dlFilter = GetCurrentFilter();
                dlFilter.LogicalOperator = "or";

                if (filter.IsGroup)
                {
                    filter.SubFilters.Add(dlFilter);
                }
                else
                {
                    var cloneFilter = filter.Clone();
                    cloneFilter.IsFirst = true;

                    filter.SubFilters = new();
                    filter.SubFilters.Add(cloneFilter);
                    filter.SubFilters.Add(dlFilter);
                    filter.Comparitor = null;
                    filter.Property = null;
                    filter.Value = null;
                    filter.Values = null;
                }

                ResetFilterInputs();
                await FilterUpdate();
            }
        }

        private async Task LoadSeries()
        {

            using (LayoutService.Working("Loading"))
            {
                await RefreshSeries(this, null);

                _ = InvokeAsync(StateHasChanged);
            }
        }

        private async Task RefreshSeries(object sender, EventArgs e)
        {
            var _result = await SonarrService.GetSeriesAsync();
            if (_result.Success)
            {
                Series = _result.Results;
            }
            else
            {
                AlertMessage = _result.ErrorString;
            }

            await BuildSeriesTreeView();

            _ = InvokeAsync(StateHasChanged);
        }
        private void ResetFilterInputs()
        {
            FilterPropertyStr = "Title";
            FilterComparitorStr = "==";
            FilterValue = null;
            FilterValues = new HashSet<string>();
        }

        private async void SaveFilter()
        {
            if (string.IsNullOrWhiteSpace(FilterName))
            {
                showSaveDialog = !showSaveDialog;
            }
            else
            {
                var id = await FilterManager.AddFilter(DlFilters, FilterName, MediaSource.Sonarr);
                JobManager.InitialiseJobs(FilterManager.GetFilter(id), ApplicationService.AppStoppingCancellationToken);
            }
        }
    }
}
