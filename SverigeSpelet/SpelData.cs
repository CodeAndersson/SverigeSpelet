using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;

namespace SverigeSpelet
{
    internal class SpelData
    {
        public string Namn { get; set; }
        public string Typ { get; set; } // kommun, län, etc
        public Geometry Geometri { get; set; }
    }
}
