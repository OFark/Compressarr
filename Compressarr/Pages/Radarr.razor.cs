using Compressarr.Filtering;
using Compressarr.Filtering.Models;
using Compressarr.Helpers;
using Compressarr.Services.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Pages
{
    public partial class Radarr
    {
        private bool showSaveDialog = false;

        private string AlertMessage = null;

        private string filter = "";

        private string newFilterTextButton = "Add";

        private string filterPropertyStr = "Title";
        private string filterComparitorStr = "==";
        private string filterValue;
        private IEnumerable<string> filterValues = null;

        private decimal FilterIntValue
        {
            get
            {
                return decimal.TryParse(filterValue, out var x) ? x : 0;
            }
            set
            {
                filterValue = value.ToString();
            }
        }

        private string filterName = "";

        private Guid _filterID;
        private Guid FilterID
        {
            get
            {
                return _filterID;
            }
            set
            {
                _filterID = value;
                if (_filterID != default)
                {
                    var filter = filterManager.GetFilter(_filterID);
                    if (filter != null)
                    {
                        filterName = filter.Name;
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
        private MudTable<Movie> table;

        private IEnumerable<Movie> movies = new HashSet<Movie>();

        private FilterProperty FilterProperty => filterManager.RadarrFilterProperties.FirstOrDefault(x => x.Value == filterPropertyStr);
        private FilterComparitor FilterComparitor => filterManager.GetComparitors(FilterProperty).FirstOrDefault(x => x.Value == filterComparitorStr);

        private List<DynamicLinqFilter> DlFilters { get; set; }

        [CascadingParameter]
        private ElementReference DivContent { get; set; }

        protected override void OnInitialized()
        {
            DlFilters = new List<DynamicLinqFilter>();

            base.OnInitialized();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await LoadMovies();
            }
        }

        private async void AddFilter()
        {

            if (FilterProperty != null && FilterComparitor != null && !string.IsNullOrWhiteSpace(filterValue))
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

        private async void ClearFilters()
        {
            DlFilters = new List<DynamicLinqFilter>();
            filterName = null;
            FilterID = default;
            await FilterUpdate();
        }

        private void FilterBy(KeyValuePair<string, object> filter)
        {
            filterPropertyStr = filter.Key;
            filterValue = filter.Value.ToString();
        }

        private void FilterOn(string filter)
        {
            filterPropertyStr = filter;
            filterValue = null;
            filterValues = null;
        }

        private DynamicLinqFilter GetCurrentFilter() => new(FilterProperty, FilterComparitor, filterValue, filterValues);



        private async void GroupFilter(DynamicLinqFilter filter)
        {

            if (FilterProperty != null && FilterComparitor != null && !string.IsNullOrWhiteSpace(filterValue))
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

                    filter.SubFilters = new ()
                    {
                        cloneFilter,
                        dlFilter
                    };
                    filter.Comparitor = null;
                    filter.Property = null;
                    filter.Value = null;
                    filter.Values = null;
                }

                ResetFilterInputs();
                await FilterUpdate();
            }
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
                if (!string.IsNullOrWhiteSpace(filterName))
                {
                    await filterManager.DeleteFilter(filterName);
                    filterName = null;
                    showSaveDialog = false;
                    ClearFilters();
                }
            }
        }

        private async void SaveFilter()
        {
            if (string.IsNullOrWhiteSpace(filterName))
            {
                showSaveDialog = !showSaveDialog;
            }
            else
            {
                var id = await filterManager.AddFilter(DlFilters, filterName, MediaSource.Radarr);
                JobManager.InitialiseJobs(filterManager.GetFilter(id), applicationService.AppStoppingCancellationToken);
            }
        }

        private void ResetFilterInputs()
        {
            filterPropertyStr = "Title";
            filterComparitorStr = "==";
            filterValue = null;
        }

        private async Task FilterUpdate()
        {
            List<string> filterArguments;

            if (DlFilters.Any())
            {
                filter = filterManager.ConstructFilterQuery(DlFilters, out filterArguments);
                //var formatStr = System.Text.RegularExpressions.Regex.Replace(filter, @"@(\d)", "\"{$1}\"");
                newFilterTextButton = "And";
            }
            else
            {
                filter = string.Empty;
                filterArguments = new();
                newFilterTextButton = "Add";
            }

            radarrService.MovieFilter = filter;
            radarrService.MovieFilterValues = filterArguments;

            await RefreshMovies(this, null);

            StateHasChanged();
        }

        private async Task RefreshMovies(object sender, EventArgs e)
        {
            var _result = await radarrService.GetMoviesAsync();
            if (_result.Success)
            {
                movies = _result.Results;
            }
            else
            {
                AlertMessage = _result.ErrorString;
            }

            _ = InvokeAsync(StateHasChanged);
        }

        private async Task LoadMovies()
        {


            using (layoutService.Working("Loading"))
            {
                await RefreshMovies(this, null);

                _ = InvokeAsync(StateHasChanged);
            }
        }
    }
}
