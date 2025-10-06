using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SverigeSpelet
{
    public class SverigeSpeletDockpaneViewModel : DockPane
    {
        private const string _dockPaneID = "SverigeSpelet_SverigeSpeletDockpane";
        private List<SpelData> _aktuellaFrågor = new List<SpelData>();
        private int _frågeIndex = 0;
        private int _tidKvar = 0;
        private int _maxTid = 10;
        private int _totalPoäng = 0;
        private string _spelareNamn = "Gäst";
        private string _svårighetsgrad = "Lätt";
        private DispatcherTimer _timer;

        public List<SpelResultat> Topplista { get; private set; } = new List<SpelResultat>();

        public string FrågaText { get; set; }
        public string TidKvarText { get; set; }
        public string PoängText { get; set; }
        public string FrågaRäknareText { get; set; }
        public string ResultatText { get; set; }
        public double TidProgress { get; set; }
        public bool SpelFlikaAktiv { get; set; }

        protected SverigeSpeletDockpaneViewModel() : base()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            LaddaTopplista();
        }

        public static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Activate();
        }

        #region Spellogik

        public async Task StartaSpel()
        {
            _frågeIndex = 0;
            _totalPoäng = 0;
            _aktuellaFrågor.Clear();

            // FIX CS4014: Lägg till await
            await FrameworkApplication.SetCurrentToolAsync("SverigeSpelet_SverigeSpeletMapTool");

            await InitieraSpel();
            SpelFlikaAktiv = true;
            NotifyPropertyChanged(nameof(SpelFlikaAktiv));

            NästaFråga();
        }

        private async Task InitieraSpel()
        {
            var featureLayer = await HämtaFeatureLayer("Kommuner");
            var allaSpelData = await SkapaSpelData(featureLayer);
            _aktuellaFrågor = BlandaOchVäljFrågor(allaSpelData, 10);
        }

        private void NästaFråga()
        {
            if (_frågeIndex >= _aktuellaFrågor.Count)
            {
                AvslutaSpel();
                return;
            }

            var aktuellFråga = _aktuellaFrågor[_frågeIndex];
            FrågaText = $"Var ligger {aktuellFråga.Namn}?";
            FrågaRäknareText = $"Fråga {_frågeIndex + 1} av {_aktuellaFrågor.Count}";

            NotifyPropertyChanged(nameof(FrågaText));
            NotifyPropertyChanged(nameof(FrågaRäknareText));

            StartaTimer();
        }

        private void StartaTimer()
        {
            _maxTid = _svårighetsgrad switch
            {
                "Lätt" => 15,
                "Medel" => 10,
                "Svår" => 5,
                _ => 10
            };

            _tidKvar = _maxTid;
            TidProgress = 100;
            TidKvarText = $"{_tidKvar} sekunder kvar";

            NotifyPropertyChanged(nameof(TidProgress));
            NotifyPropertyChanged(nameof(TidKvarText));

            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _tidKvar--;
            TidProgress = (_tidKvar / (double)_maxTid) * 100;
            TidKvarText = $"{_tidKvar} sekunder kvar";

            NotifyPropertyChanged(nameof(TidProgress));
            NotifyPropertyChanged(nameof(TidKvarText));

            if (_tidKvar <= 0)
            {
                _timer.Stop();
                HanteraSvar(false);
            }
        }

        private void HanteraSvar(bool ärRätt)
        {
            _timer.Stop();

            int poäng = 0;
            if (ärRätt)
            {
                poäng = BeräknaPoäng(_tidKvar, _svårighetsgrad);
                ResultatText = $"Rätt! +{poäng} poäng";
            }
            else
            {
                ResultatText = "Fel! 0 poäng";
            }

            _totalPoäng += poäng;
            PoängText = $"Totalpoäng: {_totalPoäng}";

            NotifyPropertyChanged(nameof(ResultatText));
            NotifyPropertyChanged(nameof(PoängText));

            Task.Delay(2000).ContinueWith(t =>
            {
                _frågeIndex++;
                NästaFråga();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private int BeräknaPoäng(int tidKvar, string svårighetsgrad)
        {
            int basPoäng = svårighetsgrad switch
            {
                "Lätt" => 10,
                "Medel" => 20,
                "Svår" => 30,
                _ => 10
            };

            int bonus = (int)(tidKvar * 0.5);
            return basPoäng + bonus;
        }

        private async void AvslutaSpel()
        {
            _timer.Stop();
            SpelFlikaAktiv = false;

            // FIX CS4014: Lägg till await
            await FrameworkApplication.SetCurrentToolAsync(null);

            SparaResultat();
            UppdateraTopplista();

            ResultatText = $"Spelet avslutat! Slutpoäng: {_totalPoäng}";
            NotifyPropertyChanged(nameof(ResultatText));
            NotifyPropertyChanged(nameof(SpelFlikaAktiv));
        }

        #endregion

        #region Kartinteraktion

        public void HanteraKartKlick(MapPoint mapClickPoint)
        {
            if (!SpelFlikaAktiv || _frågeIndex >= _aktuellaFrågor.Count || _aktuellaFrågor.Count == 0)
                return;

            try
            {
                var aktuellFråga = _aktuellaFrågor[_frågeIndex];
                var avstånd = CalculateDistance(mapClickPoint, aktuellFråga.Geometri);
                var ärRätt = avstånd < GetTolerans(_svårighetsgrad);
                HanteraSvar(ärRätt);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fel vid hantering av kartklick: {ex.Message}");
            }
        }

        private double CalculateDistance(MapPoint clickedPoint, Geometry targetGeometry)
        {
            try
            {
                if (clickedPoint == null || targetGeometry == null)
                    return double.MaxValue;

                MapPoint targetPoint;
                if (targetGeometry is Polygon polygon)
                {
                    targetPoint = GeometryEngine.Instance.Centroid(polygon) as MapPoint;
                }
                else if (targetGeometry is MapPoint mp)
                {
                    targetPoint = mp;
                }
                else
                {
                    return double.MaxValue;
                }

                if (targetPoint == null)
                    return double.MaxValue;

                var deltaX = clickedPoint.X - targetPoint.X;
                var deltaY = clickedPoint.Y - targetPoint.Y;
                var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                return distance;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fel i CalculateDistance: {ex.Message}");
                return double.MaxValue;
            }
        }

        private double GetTolerans(string svårighetsgrad)
        {
            return svårighetsgrad switch
            {
                "Lätt" => 50000,
                "Medel" => 30000,
                "Svår" => 10000,
                _ => 30000
            };
        }

        #endregion

        #region Topplista

        private void LaddaTopplista()
        {
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SverigeSpelet", "topplista.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    Topplista = JsonSerializer.Deserialize<List<SpelResultat>>(json) ?? new List<SpelResultat>();
                }
            }
            catch
            {
                Topplista = new List<SpelResultat>();
            }
        }

        private void SparaResultat()
        {
            var resultat = new SpelResultat
            {
                SpelareNamn = _spelareNamn,
                Poäng = _totalPoäng,
                Datum = DateTime.Now,
                Svårighetsgrad = _svårighetsgrad
            };

            Topplista.Add(resultat);
            Topplista = Topplista.OrderByDescending(r => r.Poäng).Take(10).ToList();

            try
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SverigeSpelet");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "topplista.json");
                var json = JsonSerializer.Serialize(Topplista);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fel vid sparning: {ex.Message}");
            }
        }

        public void UppdateraTopplista()
        {
            NotifyPropertyChanged(nameof(Topplista));
        }

        #endregion

        #region Hjälpmetoder

        private async Task<FeatureLayer> HämtaFeatureLayer(string layerNamn)
        {
            return await Task.Run(() => (FeatureLayer)null);
        }

        private async Task<List<SpelData>> SkapaSpelData(FeatureLayer layer)
        {
            return await Task.Run(() => new List<SpelData>());
        }

        private List<SpelData> BlandaOchVäljFrågor(List<SpelData> allaData, int antal)
        {
            var random = new Random();
            return allaData.OrderBy(x => random.Next()).Take(antal).ToList();
        }

        #endregion
    }
}

    