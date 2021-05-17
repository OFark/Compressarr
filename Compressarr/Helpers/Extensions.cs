using Compressarr.FFmpegFactory.Models;
using Compressarr.Settings.FFmpegFactory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Compressarr.Helpers
{
    public static class ExtensionMethods
    {
        public static string Adorn(this object text, string adornment)
        {
            return string.IsNullOrWhiteSpace(text?.ToString()) ? string.Empty : $"{text}{adornment}";
        }

        /// <summary>
        /// Perform a deep Copy of the object, using Json as a serialization method. NOTE: Private members are not cloned using this method.
        /// </summary>
        /// <typeparam name="T">The type of object being copied.</typeparam>
        /// <param name="source">The object instance to copy.</param>
        /// <returns>The copied object.</returns>
        public static T Clone<T>(this T source)
        {
            // Don't serialize a null object, simply return the default for that object
            if (source is null)
            {
                return default;
            }

            // initialize inner objects individually
            // for example in default constructor some list property initialized with some values,
            // but in 'source' these items are cleaned -
            // without ObjectCreationHandling.Replace default constructor values will be added to result
            var deserializeSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source), deserializeSettings);
        }

        public static T Clone<T>(this object source)
        {
            if (source is null)
            {
                return default;
            }
            var deserializeSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source), deserializeSettings);
        }

        public static SortedSet<Codec> Decoders(this SortedSet<Codec> codecs)
        {
            return new SortedSet<Codec>(codecs.Where(x => x.Decoder));
        }

        public static SortedSet<Codec> Encoders(this SortedSet<Codec> codecs)
        {
            return new SortedSet<Codec>(codecs.Where(x => x.Encoder));
        }

        public static string NullIfEmpty(this string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        public static string ToBitRate(this long l)
        {
            return String.Format(new FileSizeFormatProvider(), "{0:br}", l);
        }

        public static string ToBitRate(this int l)
        {
            return String.Format(new FileSizeFormatProvider(), "{0:br}", l);
        }

        public static string ToCamelCaseSplit(this string text)
        {
            return Regex.Replace(text, "([A-Z])", " $1", RegexOptions.Compiled).Trim();
        }
        public static string ToFileSize(this long l)
        {
            return String.Format(new FileSizeFormatProvider(), "{0:fs}", l);
        }

        public static string ToFileSize(this int l)
        {
            return String.Format(new FileSizeFormatProvider(), "{0:fs}", l);
        }

        public static string ToPercent(this decimal? percent, int decimals = 0)
        {
            return percent.HasValue ? Math.Round(percent.Value * 100, decimals).ToString() : null;
        }
        public static bool TryMatch(this Regex reg, string input, out Match match)
        {
            match = reg.Match(input);
            return match.Success;
        }

        public static string Wrap(this string text, string format)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            return string.Format(format, text);
        }

        public static HashSet<EncoderOptionValue> WithValues(this HashSet<EncoderOption> encoderOptions, IEnumerable<EncoderOptionValueBase> values = null)
        {
            var eovs = new HashSet<EncoderOptionValue>();
            foreach (var eo in encoderOptions)
            {
                var eov = new EncoderOptionValue(eo.Name);
                var value = values?.FirstOrDefault(v => v.Name == eo.Name);
                if (value != null)
                {
                    eov = new(value);
                }
                eov.EncoderOption = eo;

                eovs.Add(eov);
            }
            return eovs;
        }
    }
}