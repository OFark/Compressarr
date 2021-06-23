using Compressarr.Helpers;
using Compressarr.Presets;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Compressarr.FFmpeg.Models
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "matches JSON source")]
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "matches JSON source")]
    public class FFProbeResponse
    {
        [JsonIgnore]
        public IEnumerable<Stream> AttachmentStreams => streams?.Where(x => x != null && x.codec_type == CodecType.Attachment);

        [JsonIgnore]
        public IEnumerable<Stream> AudioStreams => streams?.Where(x => x != null && x.codec_type == CodecType.Audio);

        [JsonIgnore]
        public IEnumerable<Stream> DataStreams => streams?.Where(x => x != null && x.codec_type == CodecType.Data);

        public Format format { get; set; }
        public List<Stream> streams { get; set; }
        [JsonIgnore]
        public IEnumerable<Stream> SubtitleStreams => streams?.Where(x => x != null && x.codec_type == CodecType.Subtitle);
        [JsonIgnore]
        public IEnumerable<Stream> VideoStreams => streams?.Where(x => x != null && x.codec_type == CodecType.Video);

        public int GetStableHash()
        {
            return JsonConvert.SerializeObject(this).GetStableHashCode();
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "matches JSON source")]
    public class Format
    {
        public string bit_rate { get; set; }
        public string duration { get; set; }
        public TimeSpan Duration => TimeSpan.FromSeconds(double.TryParse(duration, out var dur) ? dur : 0);
            
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "matches JSON source")]
    public class SideDataList
    {
        public int inverted { get; set; }
        public string side_data_type { get; set; }
        public string type { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "matches JSON source")]
    public class Stream
    {
        public string avg_frame_rate { get; set; }
        public string bit_rate { get; set; }
        public string bits_per_raw_sample { get; set; }
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

        [JsonConverter(typeof(StringEnumConverter))]
        public CodecType codec_type { get; set; }
        public int coded_height { get; set; }
        public int coded_width { get; set; }
        public string color_primaries { get; set; }
        public string color_range { get; set; }
        public string color_space { get; set; }
        public string color_transfer { get; set; }
        public string display_aspect_ratio { get; set; }
        public Disposition disposition { get; set; }
        public string divx_packed { get; set; }
        public string dmix_mode { get; set; }
        public string duration { get; set; }
        public int? duration_ts { get; set; }
        public string field_order { get; set; }
        [JsonConverter(typeof(BoolConverter))]
        public bool has_b_frames { get; set; }

        public int height { get; set; }
        public string id { get; set; }
        public int index { get; set; }
        public string is_avc { get; set; }
        public int level { get; set; }
        public string loro_cmixlev { get; set; }
        public string loro_surmixlev { get; set; }
        public string ltrt_cmixlev { get; set; }
        public string ltrt_surmixlev { get; set; }
        public string max_bit_rate { get; set; }
        public string nal_length_size { get; set; }
        public string nb_frames { get; set; }
        public string pix_fmt { get; set; }
        public string profile { get; set; }
        public string quarter_sample { get; set; }
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "matches JSON source")]
    public class Tags
    {
        public string _STATISTICS_TAGS { get; set; }
        public string _STATISTICS_WRITING_APP { get; set; }
        public string _STATISTICS_WRITING_DATE_UTC { get; set; }
        public string album { get; set; }
        public string artist { get; set; }
        public string BPS { get; set; }
        public string comment { get; set; }
        public string compatible_brands { get; set; }
        public DateTime? creation_time { get; set; }
        public string date { get; set; }
        public string DATE_RELEASED { get; set; }
        public string description { get; set; }
        public string DeviceConformanceTemplate { get; set; }
        [JsonProperty("DISPOSITION:DEFAULT")]
        public string DISPOSITIONDEFAULT { get; set; }

        public string Duration { get; set; }
        public string encoder { get; set; }
        public string ENCODER { get; set; }
        public string EncodingGui { get; set; }
        public string filename { get; set; }
        public string genre { get; set; }
        public string handler_name { get; set; }
        public string HANDLER_NAME { get; set; }
        public string hd_video { get; set; }
        public string IAS1 { get; set; }
        public string IMDB { get; set; }
        public string IsVBR { get; set; }
        public string iTunEXTC { get; set; }
        public string iTunMOVI { get; set; }
        public string JUNK { get; set; }
        public string language { get; set; }
        public string major_brand { get; set; }
        public string media_type { get; set; }
        public string mimetype { get; set; }
        public string minor_version { get; set; }
        public string NUMBER_OF_BYTES { get; set; }
        public string NUMBER_OF_FRAMES { get; set; }
        public string stereo_mode { get; set; }
        public string synopsis { get; set; }
        public string title { get; set; }
        public string TMDB { get; set; }
        public string track { get; set; }
        public string VBRPeak { get; set; }
        public string WMFSDKNeeded { get; set; }
        public string WMFSDKVersion { get; set; }
        [JsonProperty("WM/WMADRCAverageReference")]
        public string WMWMADRCAverageReference { get; set; }

        [JsonProperty("WM/WMADRCAverageTarget")]
        public string WMWMADRCAverageTarget { get; set; }

        [JsonProperty("WM/WMADRCPeakReference")]
        public string WMWMADRCPeakReference { get; set; }

        [JsonProperty("WM/WMADRCPeakTarget")]
        public string WMWMADRCPeakTarget { get; set; }

        public string Writingfrontend { get; set; }
    }
}
