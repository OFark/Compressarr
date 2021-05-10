using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.FFmpegFactory.Models
{
    public class Codec : IComparable<Codec>
    {
        public Codec(string name, string description, HashSet<CodecOption> options)
        {
            Name = name;
            Description = description;
            Options = options;
        }

        public Codec()
        {
            Name = "copy";
            Description = "No change";
            Options = null;
            IsCopy = true;
        }

        [JsonIgnore]
        public string Description { get; set; }
        [JsonIgnore]
        public bool IsCopy { get; private set; }
        public string Name { get; set; }
        [JsonIgnore]
        public HashSet<CodecOption> Options { get; set; }

        public int CompareTo(Codec other)
        {
            return Name.CompareTo(other.Name);
        }

        public bool Equals(Codec other)
        {
            if (other == null) return false;
            return Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            Codec objAsCodec = obj as Codec;
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
