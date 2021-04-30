using Newtonsoft.Json;
using System;
using System.Text.RegularExpressions;

namespace Compressarr.Helpers
{
    public static class ExtensionMethods
    {

        public static string Adorn(this object text, string adornment)
        {
            return string.IsNullOrWhiteSpace(text?.ToString()) ? string.Empty : $"{text}{adornment}";
        }
        public static string ToFileSize(this long l)
        {
            return String.Format(new FileSizeFormatProvider(), "{0:fs}", l);
        }

        public static string ToBitRate(this long l)
        {
            return String.Format(new FileSizeFormatProvider(), "{0:br}", l);
        }

        public static string ToFileSize(this int l)
        {
            return String.Format(new FileSizeFormatProvider(), "{0:fs}", l);
        }

        public static string ToBitRate(this int l)
        {
            return String.Format(new FileSizeFormatProvider(), "{0:br}", l);
        }

        public static string NullIfEmpty(this string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text;
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

        public static bool TryMatch(this Regex reg, string input, out Match match)
        {
            match = reg.Match(input);
            return match.Success;
        }
    }
}