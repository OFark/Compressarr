using Compressarr.Application;
using Compressarr.Settings.FFmpegFactory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Compressarr.Presets.Models
{
    public class Encoder : IComparable<Encoder>, ICloneable<Encoder>
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

        public Encoder Clone()
        {
            return new Encoder(Name, Description, Options);
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
            if (obj is not Encoder objAsCodec) return false;
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
