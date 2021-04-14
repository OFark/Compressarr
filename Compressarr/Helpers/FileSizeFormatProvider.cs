using System;

namespace Compressarr.Helpers
{
    public class FileSizeFormatProvider : IFormatProvider, ICustomFormatter
    {
        public object GetFormat(Type formatType)
        {
            if (formatType == typeof(ICustomFormatter)) return this;
            return null;
        }

        private const string fileSizeFormat = "fs";
        private const string bitstreamFormat = "br";
        private const Decimal OneKiloByte = 1024M;
        private const Decimal OneMegaByte = OneKiloByte * 1024M;
        private const Decimal OneGigaByte = OneMegaByte * 1024M;

        private bool bitstream = false;

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (format == null)
            {
                return defaultFormat(format, arg, formatProvider);
            }

            if (!format.StartsWith(fileSizeFormat))
            {
                if (format.StartsWith(bitstreamFormat))
                {
                    bitstream = true;
                }
                else
                {
                    return defaultFormat(format, arg, formatProvider);
                }
            }

            if (arg is string)
            {
                return defaultFormat(format, arg, formatProvider);
            }

            Decimal size;

            try
            {
                size = Convert.ToDecimal(arg);
            }
            catch (InvalidCastException)
            {
                return defaultFormat(format, arg, formatProvider);
            }

            string suffix;
            if (size > OneGigaByte)
            {
                size /= OneGigaByte;
                suffix = bitstream ? "Gbps" : "GB";
            }
            else if (size > OneMegaByte)
            {
                size /= OneMegaByte;
                suffix = bitstream ? "Mbps" : "MB";
            }
            else if (size > OneKiloByte)
            {
                size /= OneKiloByte;
                suffix = bitstream ? "Kbps" : "kB";
            }
            else
            {
                suffix = bitstream ? "bps" : " B";
            }

            string precision = format.Substring(2);
            if (String.IsNullOrEmpty(precision)) precision = "2";
            return String.Format("{0:N" + precision + "}{1}", size, suffix);
        }

        private static string defaultFormat(string format, object arg, IFormatProvider formatProvider)
        {
            IFormattable formattableArg = arg as IFormattable;
            if (formattableArg != null)
            {
                return formattableArg.ToString(format, formatProvider);
            }
            return arg.ToString();
        }
    }
}