using Compressarr.Filtering;
using Compressarr.Services.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Services.Models
{


    public class Season
    {

        public Season()
        {

        }

        public Season(SeasonJSON seasonJSON, EpisodeFile ef)
        {
            SeasonNumber = seasonJSON.SeasonNumber;
            Statistics = seasonJSON.Statistics;

            EpisodeFile = ef;
        }

        [Filter("Episode Files", true)]
        public EpisodeFile EpisodeFile { get; set; }

        [Filter("Season Number", FilterPropertyType.Number)]
        public int SeasonNumber { get; set; }

        [Filter("Statistics", true)]
        public Statistics Statistics { get; set; }
    }

    public partial class Series
    {
        public Series()
        {

        }

        public Series(SeriesJSON seriesJSON, Season season)
        {
            Added = seriesJSON.Added;
            Certification = seriesJSON.Certification;
            EpisodeCount = seriesJSON.EpisodeCount;
            EpisodeFileCount = seriesJSON.EpisodeFileCount;
            FirstAired = seriesJSON.FirstAired;
            Genres = seriesJSON.Genres;
            Id = seriesJSON.Id;
            Network = seriesJSON.Network;
            Path = seriesJSON.Path;
            Ratings = seriesJSON.Ratings;
            Runtime = seriesJSON.Runtime;
            SeasonCount = seriesJSON.SeasonCount;
            SeriesType = seriesJSON.SeriesType;
            SizeOnDisk = seriesJSON.SizeOnDisk;
            Status = seriesJSON.Status;
            Title = seriesJSON.Title;
            Year = seriesJSON.Year;

            Season = season;
        }

        [Filter("Added", FilterPropertyType.DateTime)]
        public DateTime Added { get; set; }

        [Filter("Certification", FilterPropertyType.Enum)]
        public string Certification { get; set; }

        [Filter("Episode Count", FilterPropertyType.Number)]
        public int EpisodeCount { get; set; }

        [Filter("Episode File Count", FilterPropertyType.Number)]
        public int EpisodeFileCount { get; set; }

        [Filter("First Aired", FilterPropertyType.DateTime)]
        public DateTime FirstAired { get; set; }
        
        public List<string> Genres { get; set; }

        public int Id { get;  set; }

        [Filter("Network", FilterPropertyType.Enum)]
        public string Network { get; set; }

        [Filter("Path")]
        public string Path { get; set; }

        [Filter("Ratings", true)]
        public Ratings Ratings { get; set; }

        [Filter("Run Time", FilterPropertyType.Number)]
        public int Runtime { get; set; }

        [Filter("Season", true)]
        public Season Season { get; set; }

        [Filter("Season Count", FilterPropertyType.Number)]
        public int SeasonCount { get; set; }

        [Filter("Series Type", FilterPropertyType.Enum)]
        public string SeriesType { get; set; }

        [Filter("Size on Disk", FilterPropertyType.Number)]
        public long SizeOnDisk { get; set; }

        [Filter("Status", FilterPropertyType.Enum)]
        public string Status { get; set; }

        [Filter("Title")]
        public string Title { get; set; }
        [Filter("Year", FilterPropertyType.Number)]
        public int Year { get; set; }
    }

    public class Statistics
    {
        [Filter("Episode Count", FilterPropertyType.Number)]
        public int EpisodeCount { get; set; }

        [Filter("Episode File Count", FilterPropertyType.Number)]
        public int EpisodeFileCount { get; set; }
        [Filter("Size on Disk", FilterPropertyType.Number)]
        public long SizeOnDisk { get; set; }
    }
}
