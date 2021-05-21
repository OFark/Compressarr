using Compressarr.Helpers;
using System;

namespace Compressarr.FFmpeg.Models
{
    public class ContainerResponse : IComparable<ContainerResponse>
    {
        public string Description { get; set; }
        public string Name { get; set; }

        public int CompareTo(ContainerResponse other)
        {
            return Name.CompareTo(other.Name);
        }

        public override string ToString() => " - ".JoinWithIfNotNull(Name, Description);
    }
}
