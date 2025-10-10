using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Internal.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace SverigeSpelet
{
    public class SverigeSpeletDockpaneViewModel : DockPane
    {
        private const string _dockPaneID = "SverigeSpelet_SverigeSpeletDockpane";
        private List<SpelData> _currentQuestions = new List<SpelData>();
        private int _questionIndex = 0;
        private int _timeLeft = 0;
        private int _maxTime = 10;
        private int _totalPoints = 0;
        private string _playerName = "Gäst";
        private string _difficultyLevel = "Lätt";
        private DispatcherTimer _timer;
        private bool _showGameView = false;

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
        public Visibility GameViewVisibility => _showGameView ? Visibility.Visible : Visibility.Collapsed;
        public Visibility SettingsViewVisibility => !_showGameView ? Visibility.Visible : Visibility.Collapsed;

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

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Spellogik

        public async Task StartaSpel()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🎮 Startar spel...");

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

                System.Diagnostics.Debug.WriteLine($"Spel startat med {_currentQuestions.Count} frågor");

                // Starta första frågan
                await NextQuestion();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fel vid start av spel: {ex.Message}");
                ShowGameView = false;
            }
        }

        private async Task InitieraSpel()
        {
            try
            {
                // Initiera kartan
                await InitieraKarta();

                // Hämta feature layer
                var featureLayer = await HamtaFeatureLayer();

                if (featureLayer != null)
                {
                    // Skapa speldata
                    var allaSpelData = await SkapaSpelData(featureLayer);
                    _currentQuestions = BlandaOchValjFragor(allaSpelData, 10);
                    System.Diagnostics.Debug.WriteLine($" Initierade {_currentQuestions.Count} frågor");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Kunde inte hämta feature layer, använder testdata");
                    _currentQuestions = SkapaTestData();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fel i Initiering av Spel: {ex.Message}");
                _currentQuestions = SkapaTestData();
            }
        }

        private async Task InitieraKarta()
        {
            try
            {
                await QueuedTask.Run(async () =>
                {
                    // Envelope för Sverige
                    var sverigeEnvelope = EnvelopeBuilder.CreateEnvelope(
                        1000000, 6000000,
                        2000000, 8000000,
                        SpatialReferences.WebMercator
                    );

                    // Använd SetViewpointAsync istället för SetCurrentSketchAsync
                    await MapView.Active.SetCurrentSketchAsync(sverigeEnvelope);

                    System.Diagnostics.Debug.WriteLine("Kartan initierad med Sverige-vy");
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
                System.Diagnostics.Debug.WriteLine($"❌ Fel vid återställning av karta: {ex.Message}");
            }
        }

        private async Task NextQuestion()
        {
            await ResetMap();

            if (_questionIndex >= _currentQuestions.Count)
            {
                await AvslutaSpel();
                return;
            }

            var currentQuestion = _currentQuestions[_questionIndex];
            QuestionText = $"Var ligger {currentQuestion.Namn}?";
            QuestionCountText = $"Fråga {_questionIndex + 1} av {_currentQuestions.Count}";
            PointsText = $"Totalpoäng: {_totalPoints}";
            ResultText = "";

            NotifyPropertyChanged(nameof(QuestionText));
            NotifyPropertyChanged(nameof(QuestionCountText));
            NotifyPropertyChanged(nameof(PointsText));
            NotifyPropertyChanged(nameof(ResultText));

            StartaTimer();
        }

        private void StartaTimer()
        {
            _maxTime = _difficultyLevel switch
            {
                "Lätt" => 15,
                "Medel" => 10,
                "Svår" => 5,
                _ => 10
            };

            _timeLeft = _maxTime;
            TimeProgress = 100;
            TimeLeftText = $"{_timeLeft} sekunder kvar";

            NotifyPropertyChanged(nameof(TimeProgress));
            NotifyPropertyChanged(nameof(TimeLeftText));

            _timer.Start();
        }

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
                await HanteraSvarAsync(false, 0);
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
                ResultText = distance > 0 ? $"Fel! Du var {distance:F0} meter bort. 0 poäng" : "Fel! 0 poäng";
            }

            _totalPoints += points;
            PointsText = $"Totalpoäng: {_totalPoints}";

            NotifyPropertyChanged(nameof(ResultText));
            NotifyPropertyChanged(nameof(PointsText));

            await Task.Delay(2000);
            _questionIndex++;
            await NextQuestion();
        }

        private int CalculatedPoints(int timeLeft, string difficultyLevel)
        {
            int basePoints = difficultyLevel switch
            {
                "Lätt" => 10,
                "Medel" => 20,
                "Svår" => 30,
                _ => 10
            };

            int bonus = (int)(timeLeft * 0.5);
            return basePoints + bonus;
        }

        internal async Task AvslutaSpel()
        {
            _timer.Stop();

            // Återställ till default tool
            await FrameworkApplication.SetCurrentToolAsync(null);

            SparaResultat();
            UppdateraTopplista();

            ResultText = $"Spelet avslutat! Slutpoäng: {_totalPoints}";
            NotifyPropertyChanged(nameof(ResultText));

            await Task.Delay(3000);

            // Återgå till inställnings-vy
            ShowGameView = false;
        }

        #endregion

        #region Kartinteraktion

        internal async void HanteraKartKlick(MapPoint mapClickPoint)
        {
            if (!ShowGameView || _questionIndex >= _currentQuestions.Count)
            {
                System.Diagnostics.Debug.WriteLine("Spel-vy ej aktiv eller inga frågor");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"Kartklick: {mapClickPoint.X}, {mapClickPoint.Y}");

                var currentQuestion = _currentQuestions[_questionIndex];
                var distance = CalculateDistance(mapClickPoint, currentQuestion.Geometri);
                var isRight = distance < GetTolerans(_difficultyLevel);

                System.Diagnostics.Debug.WriteLine($"Avstånd: {distance:F0}m, Tolerans: {GetTolerans(_difficultyLevel)}m, Rätt: {isRight}");

                await HanteraSvarAsync(isRight, distance);
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

        private double GetTolerans(string difficultyLevel)
        {
            return difficultyLevel switch
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
                    TopList = JsonSerializer.Deserialize<List<SpelResultat>>(json) ?? new List<SpelResultat>();
                }
            }
            catch
            {
                TopList = new List<SpelResultat>();
            }
        }

        private void SparaResultat()
        {
            var resultat = new SpelResultat
            {
                PlayerName = _playerName,
                Points = _totalPoints,
                Date = DateTime.Now,
                DifficultyLevel = _difficultyLevel
            };

            TopList.Add(resultat);
            TopList = TopList.OrderByDescending(r => r.Points).Take(10).ToList();

            try
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SverigeSpelet");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "topplista.json");
                var json = JsonSerializer.Serialize(TopList);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fel vid sparning: {ex.Message}");
            }
        }

        public void UppdateraTopplista()
        {
            NotifyPropertyChanged(nameof(TopList));
        }

        #endregion

        #region Hjälpmetoder

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
                        // Öppna "Kommun" feature class
                        FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>("Kommun");

                        if (featureClass == null)
                        {
                            System.Diagnostics.Debug.WriteLine("Kunde inte öppna 'Kommun' feature class");
                            return null;
                        }

                        System.Diagnostics.Debug.WriteLine($"Feature class öppnad: {featureClass.GetName()}");

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
        }

        private async Task<List<SpelData>> SkapaSpelData(FeatureLayer layer)
        {
            return await QueuedTask.Run(() =>
            {
                var spelDataList = new List<SpelData>();

                if (layer == null)
                {
                    System.Diagnostics.Debug.WriteLine("Layer är null i SkapaSpelData");
                    return spelDataList;
                }

                try
                {
                    var featureClass = layer.GetFeatureClass();

                    if (featureClass == null)
                    {
                        System.Diagnostics.Debug.WriteLine("FeatureClass är null");
                        return spelDataList;
                    }

                    var queryFilter = new QueryFilter() { WhereClause = "1=1" };

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
                                    spelDataList.Add(new SpelData
                                    {
                                        Namn = namn,
                                        Geometri = geometry,
                                        Kommunkod = kommunkodStr ?? counter.ToString()
                                    });
                                    counter++;
                                }
                            }
                        }
                        System.Diagnostics.Debug.WriteLine($"Hämtade {counter} kommuner");
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
        }

        private List<SpelData> SkapaTestData()
        {
            System.Diagnostics.Debug.WriteLine("Skapar testdata...");

            return new List<SpelData>
            {
                new SpelData { Namn = "Stockholm", Kommunkod = "0180" },
                new SpelData { Namn = "Göteborg", Kommunkod = "1480" },
                new SpelData { Namn = "Malmö", Kommunkod = "1280" },
                new SpelData { Namn = "Uppsala", Kommunkod = "0380" },
                new SpelData { Namn = "Linköping", Kommunkod = "0580" },
                new SpelData { Namn = "Örebro", Kommunkod = "1880" },
                new SpelData { Namn = "Helsingborg", Kommunkod = "1283" },
                new SpelData { Namn = "Jönköping", Kommunkod = "0680" },
                new SpelData { Namn = "Umeå", Kommunkod = "2480" },
                new SpelData { Namn = "Lund", Kommunkod = "1281" }
            };
        }

        #endregion
    }
}
