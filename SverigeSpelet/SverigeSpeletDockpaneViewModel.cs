using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Editing;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Core.Internal.Geometry;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Desktop.Core;
using ArcGIS.Core.CIM;
using System.Security.Cryptography.Xml;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Internal.Mapping.Controls.MapDisplayUnitControl;
using ArcGIS.Desktop.Internal.Mapping.TOC;

namespace SverigeSpelet
{

    public class SverigeSpeletDockpaneViewModel : DockPane
    {
        private const string _dockPaneID = "SverigeSpelet_SverigeSpeletDockpane";
<<<<<<< Updated upstream
        private List<SpelData> _aktuellaFrågor = new List<SpelData>();
        private int _frågeIndex = 0;
        private int _tidKvar = 0;
        private int _maxTid = 10;
        private int _totalPoäng = 0;
        private string _spelareNamn = "Gäst";
        private string _svårighetsgrad = "Lätt";
        private DispatcherTimer _timer;
=======
        private List<SpelData> _currentQuestions = new List<SpelData>();
        private int _questionIndex = 0;
        private int _timeLeft = 0;
        private int _maxTime = 10;
        private int _totalPoints = 0;
        private string _playerName = "Gäst";
        private string _difficultyLevel = "Lätt";
        private DispatcherTimer _timer;
        private bool _showGameView = false;
>>>>>>> Stashed changes

        public List<SpelResultat> Topplista { get; private set; } = new List<SpelResultat>();

<<<<<<< Updated upstream
        public string FrågaText { get; set; }
        public string TidKvarText { get; set; }
        public string PoängText { get; set; }
        public string FrågaRäknareText { get; set; }
        public string ResultatText { get; set; }
        public double TidProgress { get; set; }
        public bool SpelFlikaAktiv { get; set; }
=======
        public List<SpelResultat> TopList { get; private set; } = new List<SpelResultat>();

        public string QuestionText { get; set; }
        public string TimeLeftText { get; set; }
        public string PointsText { get; set; }
        public string QuestionCountText { get; set; }
        public string ResultText { get; set; }
        public double TimeProgress { get; set; }
        
        public bool ShowGameView
        {
            get { return _showGameView; }
            set
            {
                _showGameView = value;
                NotifyPropertyChanged(nameof(ShowGameView));
                NotifyPropertyChanged(nameof(ShowSettingsView));
                NotifyPropertyChanged(nameof(GameViewVisibility));
                NotifyPropertyChanged(nameof(SettingsViewVisibility));
            }
        }
        public bool ShowSettingsView => !_showGameView;
        public System.Windows.Visibility GameViewVisibility =>
            _showGameView ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public System.Windows.Visibility SettingsViewVisibility =>
            !_showGameView ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
>>>>>>> Stashed changes

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
<<<<<<< Updated upstream
            _frågeIndex = 0;
            _totalPoäng = 0;
            _aktuellaFrågor.Clear();

            // FIX CS4014: Lägg till await
            await FrameworkApplication.SetCurrentToolAsync("SverigeSpelet_SverigeSpeletMapTool");

            await InitieraSpel();
            SpelFlikaAktiv = true;
            NotifyPropertyChanged(nameof(SpelFlikaAktiv));

            NästaFråga();
=======
            try
            {
                System.Diagnostics.Debug.WriteLine("Startar spel...");

                // Återställ spelstatus
                _questionIndex = 0;
                _totalPoints = 0;
                _currentQuestions.Clear();

                // Aktivera vår MapTool
                await FrameworkApplication.SetCurrentToolAsync("SverigeSpelet_SverigeSpeletMapTool");

                // Initiera spelet
                await InitieraSpel();

                // Byt till spel-vy
                ShowGameView = true;
                NotifyPropertyChanged(nameof(ShowGameView));
                NotifyPropertyChanged(nameof(ShowSettingsView));

                System.Diagnostics.Debug.WriteLine($"Spel startat med {_currentQuestions.Count} frågor");

                // Starta första frågan
                NextQuestion();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fel vid start av spel: {ex.Message}");
                ShowGameView = false; // Återgå till inställningar vid fel
            }
>>>>>>> Stashed changes
        }

        private async Task InitieraSpel()
        {
<<<<<<< Updated upstream
            var featureLayer = await HämtaFeatureLayer("Kommuner");
            var allaSpelData = await SkapaSpelData(featureLayer);
            _aktuellaFrågor = BlandaOchVäljFrågor(allaSpelData, 10);
        }

        private void NästaFråga()
        {
            if (_frågeIndex >= _aktuellaFrågor.Count)
=======
            await InitieraKarta();

            // Hämta feature layer
            var featureLayer = await HamtaFeatureLayer();

            // Skapa speldat
            var allaSpelData = await SkapaSpelData(featureLayer);

            // Välj 10 slumpmässiga frågor/ kommuner
            _currentQuestions = BlandaOchValjFragor(allaSpelData, 10);

            System.Diagnostics.Debug.WriteLine($"Initierade {_currentQuestions.Count} frågor");
        }

        private async Task InitieraKarta()
        {
            try
            {
                await QueuedTask.Run(async () =>
                {
                    // Envelope för Sverige med kord
                    var sverigeEnvelope = EnvelopeBuilder.CreateEnvelope(
                        1000000, 6000000,   // Minsta X, Y (västra/södra Sverige)
                        2000000, 8000000,   // Maxima X, Y (östra/norra Sverige)
                        SpatialReferences.WebMercator
                    );

                    await MapView.Active.SetCurrentSketchAsync(sverigeEnvelope);

                    System.Diagnostics.Debug.WriteLine("Kartan initierar med Sverige vy");
                });
            }

            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fel vid initiering av karta: {ex.Message}");
            }

        }

        internal async Task ResetMap()
        {
            try
            {
                await QueuedTask.Run(async () =>
                {
                    var sverigeEnvelope = EnvelopeBuilder.CreateEnvelope(
                        1000000, 6000000,
                        2000000, 8000000,
                        SpatialReferences.WebMercator
                    );

                    // Återställ vy till Sverige
                    await MapView.Active.SetCurrentSketchAsync(sverigeEnvelope);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fel vid återställning av karta: {ex.Message}");
            }
        }

        private async void NextQuestion()
        {   
            await ResetMap();

            if (_questionIndex >= _currentQuestions.Count)
>>>>>>> Stashed changes
            {
                AvslutaSpel();
                return;
            }

<<<<<<< Updated upstream
            var aktuellFråga = _aktuellaFrågor[_frågeIndex];
            FrågaText = $"Var ligger {aktuellFråga.Namn}?";
            FrågaRäknareText = $"Fråga {_frågeIndex + 1} av {_aktuellaFrågor.Count}";

            NotifyPropertyChanged(nameof(FrågaText));
            NotifyPropertyChanged(nameof(FrågaRäknareText));
=======
            var currentQuestion = _currentQuestions[_questionIndex];

            QuestionText = $"Var ligger {currentQuestion.Namn}?";
            QuestionCountText = $"Fråga {_questionIndex + 1} av {_currentQuestions.Count}";
            PointsText = $"Totalpoäng: {_totalPoints}";
            ResultText = ""; // Rensa tidigare resultat

            NotifyPropertyChanged(nameof(QuestionText));
            NotifyPropertyChanged(nameof(QuestionCountText));
            NotifyPropertyChanged(nameof(PointsText));
            NotifyPropertyChanged(nameof(ResultText));
>>>>>>> Stashed changes

            StartaTimer();
        }

        private void StartaTimer()
        {
<<<<<<< Updated upstream
            _maxTid = _svårighetsgrad switch
=======
            _maxTime = _difficultyLevel switch
>>>>>>> Stashed changes
            {
                "Lätt" => 15,
                "Medel" => 10,
                "Svår" => 5,
                _ => 10
            };

<<<<<<< Updated upstream
            _tidKvar = _maxTid;
            TidProgress = 100;
            TidKvarText = $"{_tidKvar} sekunder kvar";

            NotifyPropertyChanged(nameof(TidProgress));
            NotifyPropertyChanged(nameof(TidKvarText));
=======
            _timeLeft = _maxTime;
            TimeProgress = 100;
            TimeLeftText = $"{_timeLeft} sekunder kvar";

            NotifyPropertyChanged(nameof(TimeProgress));
            NotifyPropertyChanged(nameof(TimeLeftText));
>>>>>>> Stashed changes

            _timer.Start();
        }

<<<<<<< Updated upstream
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
=======
        private async void Timer_Tick(object sender, EventArgs e)
        {
            _timeLeft--;
            TimeProgress = (_timeLeft / (double)_maxTime) * 100;
            TimeLeftText = $"{_timeLeft} sekunder kvar";

            NotifyPropertyChanged(nameof(TimeProgress));
            NotifyPropertyChanged(nameof(TimeLeftText));

            if (_timeLeft <= 0)
            {
                _timer.Stop();
                await HanteraSvarAsync(false, -999);
            }
        }

        private async Task HanteraSvarAsync(bool isTrue, double distance)
        {
            _timer.Stop();

            int points = 0;
            if (isTrue)
            {
                points = CalculatedPoints(_timeLeft, _difficultyLevel);
                ResultText = $"Rätt! +{points} poäng";
            }
            else
            {
                // Korrigera avståndet för visning
                ResultText = $"Fel! Du var {distance:F0} meter bort. 0 poäng";
            }

            _totalPoints += points;
            PointsText = $"Totalpoäng: {_totalPoints}";

            NotifyPropertyChanged(nameof(ResultText));
            NotifyPropertyChanged(nameof(PointsText));

            await Task.Delay(2000);

            _questionIndex++;
            NextQuestion();

        }

        private int CalculatedPoints(int timeLeft, string difficultyLevel)
        {
            int basePoints = difficultyLevel switch
>>>>>>> Stashed changes
            {
                "Lätt" => 10,
                "Medel" => 20,
                "Svår" => 30,
                _ => 10
            };

<<<<<<< Updated upstream
            int bonus = (int)(tidKvar * 0.5);
            return basPoäng + bonus;
        }

        private async void AvslutaSpel()
        {
            _timer.Stop();
            SpelFlikaAktiv = false;

            // FIX CS4014: Lägg till await
=======
            int bonus = (int)(timeLeft * 0.5);
            return basePoints + bonus;
        }

        internal async void AvslutaSpel()
        {
            _timer.Stop();

            // Återställ till default tool
>>>>>>> Stashed changes
            await FrameworkApplication.SetCurrentToolAsync(null);

            SparaResultat();
            UppdateraTopplista();

<<<<<<< Updated upstream
            ResultatText = $"Spelet avslutat! Slutpoäng: {_totalPoäng}";
            NotifyPropertyChanged(nameof(ResultatText));
            NotifyPropertyChanged(nameof(SpelFlikaAktiv));
=======
            ResultText = $"Spelet avslutat! Slutpoäng: {_totalPoints}";
            NotifyPropertyChanged(nameof(ResultText));

            await Task.Delay(3000);
            _showGameView = false; // Återgå till inställnings vy
            NotifyPropertyChanged(nameof(ShowGameView));
            NotifyPropertyChanged(nameof(ShowSettingsView));
>>>>>>> Stashed changes
        }

        #endregion

        #region Kartinteraktion

<<<<<<< Updated upstream
        public void HanteraKartKlick(MapPoint mapClickPoint)
        {
            if (!SpelFlikaAktiv || _frågeIndex >= _aktuellaFrågor.Count || _aktuellaFrågor.Count == 0)
=======
        internal async void HanteraKartKlick(MapPoint mapClickPoint)
        {
            if (!ShowGameView || _questionIndex >= _currentQuestions.Count)
>>>>>>> Stashed changes
                return;

            try
            {
<<<<<<< Updated upstream
                var aktuellFråga = _aktuellaFrågor[_frågeIndex];
                var avstånd = CalculateDistance(mapClickPoint, aktuellFråga.Geometri);
                var ärRätt = avstånd < GetTolerans(_svårighetsgrad);
                HanteraSvar(ärRätt);
=======
                var currentQuestion = _currentQuestions[_questionIndex];
                var distance = CalculateDistance(mapClickPoint, currentQuestion.Geometri);
                var isRight = distance < GetTolerans(_difficultyLevel);

                // Rita linje mellan gissning och rätt svar
                await RitaLinje(mapClickPoint, currentQuestion.Geometri, distance);

                await HanteraSvarAsync(isRight, distance);
>>>>>>> Stashed changes
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fel vid hantering av kartklick: {ex.Message}");
            }
        }

<<<<<<< Updated upstream
=======
        private async Task RitaLinje(MapPoint fromPoint, Geometry toGeometry, double distance)
        {
            await QueuedTask.Run(() =>
            {
                // Skapa en linje mellan punkterna
                var line = PolylineBuilder.CreatePolyline(new[] { fromPoint,
            GeometryEngine.Instance.Centroid(toGeometry) as MapPoint });

                // Skapa en grafisk linje för visualisering
                // Implementera grafikhäntering här

            });
        }

>>>>>>> Stashed changes
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
<<<<<<< Updated upstream
                System.Diagnostics.Debug.WriteLine($"Fel i CalculateDistance: {ex.Message}");
=======
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"Fel i CalculateDistance: {ex.Message}");
>>>>>>> Stashed changes
                return double.MaxValue;
            }
        }

<<<<<<< Updated upstream
        private double GetTolerans(string svårighetsgrad)
        {
            return svårighetsgrad switch
=======
        private double GetTolerans(string difficultyLevel)
        {
            return difficultyLevel switch
>>>>>>> Stashed changes
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
<<<<<<< Updated upstream
                    Topplista = JsonSerializer.Deserialize<List<SpelResultat>>(json) ?? new List<SpelResultat>();
=======
                    TopList = JsonSerializer.Deserialize<List<SpelResultat>>(json) ?? new List<SpelResultat>();
>>>>>>> Stashed changes
                }
            }
            catch
            {
<<<<<<< Updated upstream
                Topplista = new List<SpelResultat>();
=======
                TopList = new List<SpelResultat>();
>>>>>>> Stashed changes
            }
        }

        private void SparaResultat()
        {
            var resultat = new SpelResultat
            {
<<<<<<< Updated upstream
                SpelareNamn = _spelareNamn,
                Poäng = _totalPoäng,
                Datum = DateTime.Now,
                Svårighetsgrad = _svårighetsgrad
            };

            Topplista.Add(resultat);
            Topplista = Topplista.OrderByDescending(r => r.Poäng).Take(10).ToList();
=======
                PlayerName = _playerName,
                Points = _totalPoints,
                Date = DateTime.Now,
                DifficultyLevel = _difficultyLevel
            };

            TopList.Add(resultat);
            TopList = TopList.OrderByDescending(r => r.Points).Take(10).ToList();
>>>>>>> Stashed changes

            try
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SverigeSpelet");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "topplista.json");
<<<<<<< Updated upstream
                var json = JsonSerializer.Serialize(Topplista);
=======
                var json = JsonSerializer.Serialize(TopList);
>>>>>>> Stashed changes
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fel vid sparning: {ex.Message}");
            }
        }

        public void UppdateraTopplista()
        {
<<<<<<< Updated upstream
            NotifyPropertyChanged(nameof(Topplista));
=======
            NotifyPropertyChanged(nameof(TopList));
>>>>>>> Stashed changes
        }

        #endregion

        #region Hjälpmetoder

<<<<<<< Updated upstream
        private async Task<FeatureLayer> HämtaFeatureLayer(string layerNamn)
        {
            return await Task.Run(() => (FeatureLayer)null);
=======
        private async Task<FeatureLayer> HamtaFeatureLayer()
        {
            return await QueuedTask.Run(() =>
            {
                try
                {
                    string databasePath = @"C:\Quiz.gdb";

                    System.Diagnostics.Debug.WriteLine($"Öppnar geodatabas: {databasePath}");

                    using (Geodatabase geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(databasePath))))
                    {
                        // Öppna specifikt "Kommun" feature class
                        FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>("Kommun");

                        if (featureClass == null)
                        {
                            System.Diagnostics.Debug.WriteLine("Kunde inte öppna 'Kommun' feature class");
                            return null;
                        }

                        System.Diagnostics.Debug.WriteLine($"Öppnade feature class: {featureClass.GetName()}");

                        // Skapa lager
                        Layer layer = LayerFactory.Instance.CreateLayer<FeatureLayer>(
                            new FeatureLayerCreationParams(featureClass),
                            MapView.Active.Map);

                        System.Diagnostics.Debug.WriteLine($"Layer skapad: {layer?.Name}");
                        return layer as FeatureLayer;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fel vid hämtning av feature layer: {ex.Message}");
                    return null;
                }
            });
>>>>>>> Stashed changes
        }

        private async Task<List<SpelData>> SkapaSpelData(FeatureLayer layer)
        {
<<<<<<< Updated upstream
            return await Task.Run(() => new List<SpelData>());
        }

        private List<SpelData> BlandaOchVäljFrågor(List<SpelData> allaData, int antal)
        {
            var random = new Random();
            return allaData.OrderBy(x => random.Next()).Take(antal).ToList();
=======
            return await QueuedTask.Run(() =>
            {
                var spelDataList = new List<SpelData>();

                try
                {
                    var featureClass = layer.GetFeatureClass();

                    var fieldNames = featureClass.GetDefinition().GetFields().Select(f => f.Name).ToList();
                    System.Diagnostics.Debug.WriteLine($"SKAPELSEDATA: Bearbetar {fieldNames.Count} fält");

                    var queryFilter = new QueryFilter()
                    {
                        WhereClause = "1=1"
                    };

                    using (var rowCursor = featureClass.Search(queryFilter))
                    {
                        int counter = 0;
                        while (rowCursor.MoveNext())
                        {
                            using (Feature feature = rowCursor.Current as Feature)
                            {
                                var geometry = feature.GetShape();
                                var namn = feature["NAMN"]?.ToString();
                                var kommunkodStr = feature["KOMMUNKOD"]?.ToString();

                                if (geometry != null && !string.IsNullOrEmpty(namn))
                                {
                                    var kommunkod = int.TryParse(kommunkodStr, out int result) ? result : counter;

                                    spelDataList.Add(new SpelData
                                    {
                                        Namn = namn,
                                        Geometri = geometry,
                                        Kommunkod = kommunkod.ToString()
                                    });
                                    counter++;
                                }
                            }
                        }
                        System.Diagnostics.Debug.WriteLine($"Hämtade {counter} kommuner med korrekta fält");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fel vid skapande av speldata: {ex.Message}");
                }

                return spelDataList;
            });
        }
        private List<SpelData> BlandaOchValjFragor(List<SpelData> allaData, int antalFragor)
        {
            var random = new Random();
            return allaData.OrderBy(x => random.Next()).Take(antalFragor).ToList();
>>>>>>> Stashed changes
        }

        #endregion
    }
}

<<<<<<< Updated upstream
    
=======

>>>>>>> Stashed changes
