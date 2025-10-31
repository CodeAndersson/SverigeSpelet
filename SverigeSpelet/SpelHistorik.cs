using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;

namespace SverigeSpelet
{
    public class SpelHistorik
    {
        public string Fråga { get; set; }
        public string Kommun { get; set; }
        public bool VarRätt { get; set; }
        public int Poäng { get; set; }
        public double Avstånd { get; set; }
        public MapPoint KorrektPlats { get; set; }
        public MapPoint AnvändarensGissning { get; set; }
        public DateTime Tidpunkt { get; set; } = DateTime.Now;
    }

}
