using Compressarr.Presets.Models;
using Compressarr.Settings.FFmpegFactory;
using Microsoft.AspNetCore.Components;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        public static SortedSet<Codec> Decoders(this SortedSet<Codec> codecs)
        {
            return new SortedSet<Codec>(codecs.Where(x => x.Decoder));
        }

        public static SortedSet<Codec> Encoders(this SortedSet<Codec> codecs)
        {
            return new SortedSet<Codec>(codecs.Where(x => x.Encoder));
        }

        /// <summary>
        /// Gets a Deterministic hash for stability between app domains. (GetHashCode is randomised between app runtimes)
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static int GetStableHashCode(this string str)
        {
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }

        /// <summary>
        /// Perform a deep Copy of the object, using Json as a serialization method. NOTE: Private members are not cloned using this method.
        /// </summary>
        /// <typeparam name="T">The type of object being copied.</typeparam>
        /// <param name="source">The object instance to copy.</param>
        /// <returns>The copied object.</returns>
        public static T JsonClone<T>(this T source)
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

        public static T JsonClone<T>(this object source)
        {
            if (source is null)
            {
                return default;
            }
            var deserializeSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source), deserializeSettings);
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

        public static void Clear<T>(this EventHandler<T> eh)
        {
            foreach (Delegate d in eh.GetInvocationList())
            {
                eh -= (EventHandler<T>)d;
            }
        }

        public static string ToFileSize(this int l)
        {
            return String.Format(new FileSizeFormatProvider(), "{0:fs}", l);
        }

        public static string ToPercent(this decimal percent, int decimals = 0)
        {
            return Math.Round(percent * 100, decimals).ToString();
        }

        public static string Capitalise(this string text)
        {
            return text switch
            {
                null => null,
                "" => string.Empty,
                _ => text.First().ToString().ToUpper() + text[1..]
            };
        }

        public static string ToPercent(this decimal? percent, int decimals = 0)
        {
            return percent.HasValue ? ToPercent(percent.Value, decimals).ToString() : null;
        }

        public static string ToStringTimeSeconds(this TimeSpan span)
        {
            return span.ToString(@"hh\:mm\:ss");
        }
        public static bool TryMatch(this Regex reg, string input, out Match match)
        {
            match = reg.Match(input);
            return match.Success;
        }

        public static string JoinWithIfNotNull(this string seperator, params string[] values) => string.Join(seperator, values.Where(x => !string.IsNullOrWhiteSpace(x)));

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

        public static string Wrap(this object text, string format)
        {
            if (text == null || string.IsNullOrWhiteSpace(text.ToString())) return null;
            return string.Format(format, text);
        }
    }
}