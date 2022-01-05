using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.Presets.Models
{
    public class Codec : IComparable<Codec>
    {
        public Codec(string name, string description, bool decoder, bool encoder)
        {
            Name = name;
            Description = description;
            Decoder = decoder;
            Encoder = encoder;
        }

        public bool Decoder { get; set; }
        public string Description { get; set; }
        public bool Encoder { get; set; }

        public string Name { get; set; }

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
            if(obj is not null and Codec objAsCodec)
                return Equals(objAsCodec);

            return false;
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
