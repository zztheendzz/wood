using System;
using System.Collections.Generic;
using System.Text;

namespace wood.Models
{
    public class TextureResult
    {
        public double TextureMean { get;internal set; }
        public double TextureDeviation { get; internal set; }
        public double SaturationMean { get; internal set; }
        public double SaturationDeviation { get; internal set; }
        public double GrayMean { get; internal set; }
        public double GrayDeviation { get; internal set; }
        
        public string Merterial { get; set; }

        public TextureResult() { }

    }
}
