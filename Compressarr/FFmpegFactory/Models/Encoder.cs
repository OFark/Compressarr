using Compressarr.Settings.FFmpegFactory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.FFmpegFactory.Models
{
    public class Encoder : IComparable<Encoder>
    {
        public Encoder(string name, string description, HashSet<EncoderOption> options)
        {
            Name = name;
            Description = description;
            Options = options;
        }

        public Encoder()
        {
            Name = "copy";
            Description = "No change";
            Options = null;
            IsCopy = true;
        }

        public Encoder(EncoderBase encoder)
        {
            Name = encoder?.Name;
        }

        [JsonIgnore]
        public string Description { get; set; }

        [JsonIgnore]
        public bool IsCopy { get; private set; }
        public string Name { get; set; }
        [JsonIgnore]
        public HashSet<EncoderOption> Options { get; set; }

        public int CompareTo(Encoder other)
        {
            return Name.CompareTo(other.Name);
        }

        public bool Equals(Encoder other)
        {
            if (other == null) return false;
            return Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            Encoder objAsCodec = obj as Encoder;
            if (objAsCodec == null) return false;
            else return Equals(objAsCodec);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override string ToString()
        {
            return $"{Name} - {Description}";
        }
    }
}
