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
        public string Kommunkod { get; set; }
        public string AceID { get; set; }
        public string GLOBALID { get; set; }
        public decimal SHAPE_Length { get; set; }
        public decimal SHAPE_Area { get; set; }

    }
}
