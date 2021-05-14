using Compressarr.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.FFmpegFactory.Models
{
    public class Disposition
    {
        [JsonConverter(typeof(BoolConverter))] 
        public bool @default { get; set; }
        [JsonConverter(typeof(BoolConverter))]
        public bool attached_pic { get; set; }

        [JsonConverter(typeof(BoolConverter))]
        public bool clean_effects { get; set; }

        [JsonConverter(typeof(BoolConverter))]
        public bool comment { get; set; }

        [JsonConverter(typeof(BoolConverter))]
        public bool dub { get; set; }
        [JsonConverter(typeof(BoolConverter))]
        public bool forced { get; set; }

        [JsonConverter(typeof(BoolConverter))]
        public bool hearing_impaired { get; set; }

        [JsonConverter(typeof(BoolConverter))]
        public bool karaoke { get; set; }

        [JsonConverter(typeof(BoolConverter))]
        public bool lyrics { get; set; }

        [JsonConverter(typeof(BoolConverter))]
        public bool original { get; set; }
        [JsonConverter(typeof(BoolConverter))]
        public bool timed_thumbnails { get; set; }

        [JsonConverter(typeof(BoolConverter))]
        public bool visual_impaired { get; set; }
    }

    public class FFProbeResponse
    {
        public Format format { get; set; }
        public List<Stream> streams { get; set; }
    }

    public class Format
    {
        public string bit_rate { get; set; }
        public string duration { get; set; }
        public string filename { get; set; }
        public string format_long_name { get; set; }
        public string format_name { get; set; }
        public int nb_programs { get; set; }
        public int nb_streams { get; set; }
        public int probe_score { get; set; }
        public string size { get; set; }
        public string start_time { get; set; }
        public Tags tags { get; set; }
    }

    public class SideDataList
    {
        public string side_data_type { get; set; }
    }

    public class Stream
    {
        public string avg_frame_rate { get; set; }
        public string bit_rate { get; set; }
        public int? bits_per_sample { get; set; }
        public string channel_layout { get; set; }
        public int? channels { get; set; }
        public string chroma_location { get; set; }
        [JsonConverter(typeof(BoolConverter))]
        public bool closed_captions { get; set; }

        public string codec_long_name { get; set; }
        public string codec_name { get; set; }
        public string codec_tag { get; set; }
        public string codec_tag_string { get; set; }
        public string codec_time_base { get; set; }
        public string codec_type { get; set; }
        public int coded_height { get; set; }
        public int coded_width { get; set; }
        public string color_range { get; set; }
        public string display_aspect_ratio { get; set; }
        public Disposition disposition { get; set; }
        public string dmix_mode { get; set; }
        public string duration { get; set; }
        public int? duration_ts { get; set; }
        public string field_order { get; set; }
        [JsonConverter(typeof(BoolConverter))]
        public bool has_b_frames { get; set; }

        public int height { get; set; }
        public int index { get; set; }
        public int level { get; set; }
        public string loro_cmixlev { get; set; }
        public string loro_surmixlev { get; set; }
        public string ltrt_cmixlev { get; set; }
        public string ltrt_surmixlev { get; set; }
        public string pix_fmt { get; set; }
        public string profile { get; set; }
        public string r_frame_rate { get; set; }
        public int refs { get; set; }
        public string sample_aspect_ratio { get; set; }
        public string sample_fmt { get; set; }
        public string sample_rate { get; set; }
        public List<SideDataList> side_data_list { get; set; }
        public int start_pts { get; set; }
        public string start_time { get; set; }
        public Tags tags { get; set; }
        public string time_base { get; set; }
        public int width { get; set; }
    }

    public class Tags
    {
        public DateTime creation_time { get; set; }
        public string encoder { get; set; }
        public string language { get; set; }
        public string title { get; set; }
    }
}
