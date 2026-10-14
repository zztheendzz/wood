using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Text;

namespace wood.Models
{
    public class RegionResult
    {
        public double Row1 { get; set; }
        public double Column1 { get; set; }
        public double Row2 { get; set; }
        public double Column2 { get; set; }

        public HRegion? Region { get; set; }
    }
}
