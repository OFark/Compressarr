﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Compressarr.FFmpegFactory.Models
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
