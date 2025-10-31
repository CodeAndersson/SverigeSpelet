using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Internal.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace SverigeSpelet
{
    public class SverigeSpeletDockpaneViewModel : DockPane, INotifyPropertyChanged
    {
        private const string _dockPaneID = "SverigeSpelet_SverigeSpeletDockpane";

        private bool _showGameView;
        private bool _showSettingsView = true;
        private string _currentQuestion;
        private string _timeLeftText;
        private string _pointsText;
        private string _questionCountText;
        private string _resultText;
        private double _timeProgress;
        private string _playerName = "Spelare";
        private string _difficultyLevel = "Medel";

        private int _questionIndex = 0;
        private int _totalPoints = 0;
        private int _maxTime = 10;
        private int _timeRemaining;

        private bool _isGameActive = false;
        private bool _isProcessingAnswer = false;
        private bool _gameOver = false;
        public string AvslutaKnappText => _gameOver ? "Gå tillbaka" : "Avsluta Spel";


        private DispatcherTimer _timer;
        private List<SpelData> _currentQuestions = new();

        private MapPoint _userGuess;
        private MapPoint _correctAnswer;
        private double _lastDistance;

        private FeatureLayer _kommunLayer;
        private CIMSymbolReference _originalSymbol;
        private string _lastAnswersedKommunName;

        private ObservableCollection<SpelHistorik> _spelHistorik = new ObservableCollection<SpelHistorik>();
        private SpelHistorik _valdHistorikPost;

        #region === Binding Properties ===
        public ObservableCollection<SpelHistorik> SpelHistorik
        {
            get => _spelHistorik;
            set => SetProperty(ref _spelHistorik, value);
        }

        public SpelHistorik ValdHistorikPost
        {
            get => _valdHistorikPost;
            set
            {
                SetProperty(ref _valdHistorikPost, value);
                if (value != null)
                {
                    _ = VisaHistorikPost(value);
                }
            }
        }

        public string HistorikSammanfattning =>
            $"Rätt: {SpelHistorik.Count(h => h.VarRätt)}/{SpelHistorik.Count} | " +
            $"Totalpoäng: {SpelHistorik.Sum(h => h.Poäng)}";

        public bool ShowSettingsView
        {
            get => _showSettingsView;
            set => SetProperty(ref _showSettingsView, value);
        }

        public bool ShowGameView
        {
            get => _showGameView;
            set => SetProperty(ref _showGameView, value);
        }

        public string CurrentQuestion
        {
            get => _currentQuestion;
            set => SetProperty(ref _currentQuestion, value);
        }

        public string TimeLeftText
        {
            get => _timeLeftText;
            set => SetProperty(ref _timeLeftText, value);
        }

        public int TotalPoints
        {
            get => _totalPoints;
            set => SetProperty(ref _totalPoints, value);
        }

        public int MaxTime
        {
            get => _maxTime;
            set => SetProperty(ref _maxTime, value);
        }

        public int TimeRemaining
        {
            get => _timeRemaining;
            set => SetProperty(ref _timeRemaining, value);
        }

        public bool GameOver
        {
            get => _gameOver;
            set => SetProperty(ref _gameOver, value);
        }

        public string PointsText
        {
            get => _pointsText;
            set => SetProperty(ref _pointsText, value);
        }

        public int QuestionIndex
        {
            get => _questionIndex;
            set => SetProperty(ref _questionIndex, value);
        }

        public string QuestionCountText
        {
            get => _questionCountText;
            set => SetProperty(ref _questionCountText, value);
        }

        public string ResultText
        {
            get => _resultText;
            set => SetProperty(ref _resultText, value);
        }

        public double TimeProgress
        {
            get => _timeProgress;
            set => SetProperty(ref _timeProgress, value);
        }

        public string PlayerName
        {
            get => _playerName;
            set => SetProperty(ref _playerName, value);
        }

        public string DifficultyLevel
        {
            get => _difficultyLevel;
            set => SetProperty(ref _difficultyLevel, value);
        }

        public bool IsGameActive
        {
            get => _isGameActive;
            set => SetProperty(ref _isGameActive, value);
        }

        public bool IsProcessingAnswer
        {
            get => _isProcessingAnswer;
            set => SetProperty(ref _isProcessingAnswer, value);
        }
        #endregion

        public string DistanceInfo => _lastDistance > 0 ? $"Avstånd: {_lastDistance:F1} km" : "";
        public string PinInfo => _userGuess != null && _correctAnswer != null ?
            $"Din gissning: ({_userGuess.X:F2}, {_userGuess.Y:F2}) | Rätt svar: ({_correctAnswer.X:F2}, {_correctAnswer.Y:F2})" : "";

        public List<SpelResultat> TopList { get; set; } = new();
        public List<string> DifficultyLevels { get; } = new List<string> { "Lätt", "Medel", "Svår" };

        // Commands
        public ICommand StartGameCommand { get; }
        public ICommand EndGameCommand { get; }
        public ICommand UpdateTopListCommand { get; }
        public ICommand ShowOnMapCommand { get;private set; }

        // Construktor
        protected SverigeSpeletDockpaneViewModel() : base()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;

            StartGameCommand = new RelayCommand(async (_) => await StartaSpel());
            EndGameCommand = new RelayCommand<object>(async (_) => await AvslutaSpel());
            UpdateTopListCommand = new RelayCommand(() => UppdateraTopplista());
            ShowOnMapCommand = new RelayCommand<SpelHistorik>(OnShowOnMap);


            LaddaTopplista();
            PointsText = "Totalpoäng: 0";
            QuestionCountText = "Fråga 0/0";
        }

        public static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }

        #region === Spellogik ===

        public async Task StartaSpel()
        {
            try
            {
                ResetGameState();
                IsGameActive = true;

                ShowSettingsView = false;
                ShowGameView = true;

                await FrameworkApplication.SetCurrentToolAsync("SverigeSpelet_SverigeSpeletMapTool");

                await InitieraSpel();
                await SetTransparentCommunitiesSimple();


                PointsText = $"Totalpoäng: {_totalPoints}";
                await NextQuestion();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fel vid start: {ex.Message}");
                ShowGameView = false;
                ShowSettingsView = true;
            }
        }

        private async Task InitieraSpel()
        {
            await InitieraKarta();
            var layer = await HamtaFeatureLayer();
            _currentQuestions = layer != null
                ? BlandaOchValjFragor(await SkapaSpelData(layer), 10)
                : new List<SpelData>();

            if (_currentQuestions.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("Varning: Inga frågor kunde skapas");
            }
        }

        private async Task NextQuestion()
        {
            await ResetMap();

            if (_questionIndex >= _currentQuestions.Count)
            {
                await VisaSlutresultat();
                return;
            }

            var q = _currentQuestions[_questionIndex];
            CurrentQuestion = $"Var ligger {q.Namn}?";
            QuestionCountText = $"Fråga {_questionIndex + 1} av {_currentQuestions.Count}";
            PointsText = $"Totalpoäng: {_totalPoints}";
            ResultText = "";

            // Rensa tidigare gissningar
            _userGuess = null;
            _correctAnswer = null;
            _lastDistance = 0;
            NotifyPropertyChanged(nameof(DistanceInfo));
            NotifyPropertyChanged(nameof(PinInfo));

            StartTimer();
        }

        private void StartTimer()
        {
            _maxTime = _difficultyLevel switch
            {
                "Lätt" => 15,
                "Medel" => 10,
                "Svår" => 5,
                _ => 10
            };

            TimeRemaining = _maxTime;
            TimeProgress = 100;
            TimeLeftText = $"{TimeRemaining} sekunder kvar";
            _timer.Start();
        }

        private async void Timer_Tick(object sender, EventArgs e)
        {
            TimeRemaining--;
            TimeProgress = (TimeRemaining / (double)_maxTime) * 100;
            TimeLeftText = $"{TimeRemaining} sekunder kvar";

            if (TimeRemaining <= 0)
            {
                _timer.Stop();

                var currentQuestion = _currentQuestions[_questionIndex];
                var center = currentQuestion.Geometri is Polygon p ?
                    GeometryEngine.Instance.Centroid(p) as MapPoint :
                    currentQuestion.Geometri as MapPoint;

                await HanteraSvarAsync(false, double.MaxValue, currentQuestion.Namn, center, null);
            }
        }

        private async Task HanteraSvarAsync(bool correct, double distance, string kommunName, MapPoint correctLocation, MapPoint userGuess)
        {
            _timer.Stop();

            await FrameworkApplication.SetCurrentToolAsync(null);

            int points = 0;
            if (correct)
            {
                points = CalculatedPoints(TimeRemaining, DifficultyLevel);
                ResultText = $"Rätt! +{points} poäng";

                await HighlightKommun(kommunName, true);
            }
            else
            {
                ResultText = distance > 0 && distance < double.MaxValue ? $"Fel! Du var {distance:F1} km bort" : "Tiden tog slut!";

                if (!string.IsNullOrEmpty(kommunName))
                {
                    await HighlightKommun(kommunName, false);
                }
            }

            TotalPoints += points;
            PointsText = $"Totalpoäng: {TotalPoints}";  

            var historikPost = new SpelHistorik
            {
                Fråga = $"Var ligger {kommunName}?",
                Kommun = kommunName,
                VarRätt = correct,
                Poäng = points,
                Avstånd = distance,
                KorrektPlats = correctLocation,
                AnvändarensGissning = userGuess,
                Tidpunkt = DateTime.Now
            };

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SpelHistorik.Add(historikPost);
                NotifyPropertyChanged(nameof(HistorikSammanfattning));
            });


            await Task.Delay(1500);

            await ResetKommunAppearance();

            QuestionIndex++;

            if (QuestionIndex < _currentQuestions.Count)
            {
                await FrameworkApplication.SetCurrentToolAsync("SverigeSpelet_SverigeSpeletMapTool");
            }

            await NextQuestion();
        }

        private int CalculatedPoints(int tidKvar, string diff)
        {
            int bas = diff switch
            {
                "Lätt" => 10,
                "Medel" => 20,
                "Svår" => 30,
                _ => 10
            };
            return bas + (int)(tidKvar * 0.5);
        }

        public async Task AvslutaSpel()
        {
            if (GameOver)
            {
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await ResetKommunAppearance();
                    await FrameworkApplication.SetCurrentToolAsync(null);

                    ShowGameView = false;
                    ShowSettingsView = true;
                    ResetGameState();
                });
            }
            else
            {
                await VisaSlutresultat();
            }

        }
        #endregion

        #region === Kartinteraktion ===

        internal async void HanteraKartKlick(MapPoint klick)
        {
            if ( !IsGameActive || !ShowGameView || QuestionIndex >= _currentQuestions.Count || IsProcessingAnswer) return;

            IsProcessingAnswer = true;

            try
            {
                var current = _currentQuestions[QuestionIndex];
                var center = current.Geometri is Polygon p ?
                    GeometryEngine.Instance.Centroid(p) as MapPoint :
                    current.Geometri as MapPoint;

                if (center == null) return;

                // Spara koordinater för visning
                _userGuess = klick;
                _correctAnswer = center;
                _lastDistance = CalculateDistance(klick, center);

                // Uppdatera UI med koordinatinformation
                NotifyPropertyChanged(nameof(DistanceInfo));
                NotifyPropertyChanged(nameof(PinInfo));

                var isRight = _lastDistance < GetTolerans(_difficultyLevel);
                await HanteraSvarAsync(isRight, _lastDistance, current.Namn, center, klick);
            }
            finally
            {
                IsProcessingAnswer = false;
            }
        }

        private double CalculateDistance(MapPoint clicked, Geometry geom)
        {
            if (clicked == null || geom == null) return double.MaxValue;
            var center = geom is Polygon p ? GeometryEngine.Instance.Centroid(p) as MapPoint : geom as MapPoint;

            if (center == null) return double.MaxValue;

            try
            {
                // Använd geodesic distance för korrekt avstånd i km
                double distanceInMeters = GeometryEngine.Instance.GeodesicDistance(clicked, center);
                return distanceInMeters / 1000.0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fel vid avståndsberäkning: {ex.Message}");
                // Fallback till enkel distance
                return GeometryEngine.Instance.Distance(clicked, center) / 1000.0;
            }
        }

        private double GetTolerans(string diff) => diff switch
        {
            "Lätt" => 50,   // 50 km
            "Medel" => 30,  // 30 km
            "Svår" => 10,   // 10 km
            _ => 30
        };

        #endregion

        #region === Topplista ===

        private void LaddaTopplista()
        {
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SverigeSpelet", "topplista.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    TopList = JsonSerializer.Deserialize<List<SpelResultat>>(json) ?? new();
                }
            }
            catch { TopList = new(); }
        }

        private void SparaResultat()
        {
            var res = new SpelResultat
            {
                PlayerName = _playerName,
                Points = _totalPoints,
                Date = DateTime.Now,
                DifficultyLevel = _difficultyLevel
            };

            TopList.Add(res);
            TopList = TopList.OrderByDescending(r => r.Points).Take(10).ToList();

            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SverigeSpelet");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "topplista.json");
            File.WriteAllText(path, JsonSerializer.Serialize(TopList));

            UppdateraTopplista();
        }

        public void UppdateraTopplista()
        {
            NotifyPropertyChanged(nameof(TopList));
        }

        #endregion

        #region === Hjälpmetoder ===

        private async Task InitieraKarta()
        {
            await QueuedTask.Run(async () =>
            {
                var env = EnvelopeBuilder.CreateEnvelope(
                    1000000, 6000000,
                    2000000, 8000000,
                    SpatialReferences.WebMercator);
                await MapView.Active.SetCurrentSketchAsync(env);
            });
        }

        private async Task ResetMap()
        {
            await InitieraKarta();
        }

        private async Task<FeatureLayer> HamtaFeatureLayer()
        {
            return await QueuedTask.Run(() =>
            {
                try
                {
                    string db = @"C:\\Quiz.gdb";
                    using var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(db)));
                    var fc = gdb.OpenDataset<FeatureClass>("Kommun");
                    return LayerFactory.Instance.CreateLayer<FeatureLayer>(
                        new FeatureLayerCreationParams(fc), MapView.Active.Map);
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
                var list = new List<SpelData>();
                try
                {
                    var fc = layer.GetFeatureClass();
                    using var cursor = fc.Search(new QueryFilter { });
                    while (cursor.MoveNext())
                    {
                        using var f = (Feature)cursor.Current;
                        var geom = f.GetShape();
                        var namn = f["NAMN"]?.ToString();
                        if (geom != null && !string.IsNullOrEmpty(namn))
                            list.Add(new SpelData { Namn = namn, Geometri = geom });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fel vid skapande av speldata: {ex.Message}");
                }
                return list;
            });
        }

        private void ResetGameState()
        {
            IsGameActive = false;
            IsProcessingAnswer = false;

            QuestionIndex = 0;
            TotalPoints = 0;
            _currentQuestions.Clear();
            _userGuess = null;
            _correctAnswer = null;
            _lastDistance = 0;
            GameOver = false;

            SpelHistorik.Clear();
            NotifyPropertyChanged(nameof(HistorikSammanfattning));
            NotifyPropertyChanged(nameof(AvslutaKnappText));

            PointsText = "Totalpoäng: 0";
            QuestionCountText = "Fråga 0/0";
            ResultText = "";
        }

        private List<SpelData> BlandaOchValjFragor(List<SpelData> alla, int antal)
        {
            var rnd = new Random();
            return alla.OrderBy(_ => rnd.Next()).Take(antal).ToList();
        }

        private async Task VisaSlutresultat()
        {
            _timer.Stop();
            IsGameActive = false;

            await FrameworkApplication.SetCurrentToolAsync(null);
            await ResetKommunAppearance();

            SparaResultat();
            UppdateraTopplista();

            var antalRätt = SpelHistorik.Count(h => h.VarRätt);
            var antalTotalt = SpelHistorik.Count;

            ResultText = $"Spelet är slut!\nRätt: {antalRätt}/{antalTotalt}\nTotalpoäng: {TotalPoints}\nKlicka 'Gå tillbaka' för att spela igen";

            GameOver = true;
            NotifyPropertyChanged(nameof(AvslutaKnappText));
        }

        private async Task SetTransparentCommunitiesSimple()
        {
            await QueuedTask.Run(() =>
            {
                try
                {
                    // Hitta och spara kommun-lagret
                    _kommunLayer = MapView.Active.Map.GetLayersAsFlattenedList()
                        .OfType<FeatureLayer>()
                        .FirstOrDefault(layer => layer.Name == "Kommun" || layer.Name.Contains("Kommun"));

                    if (_kommunLayer != null)
                    {
                        // Spara original-symbolen
                        var renderer = _kommunLayer.GetRenderer() as CIMSimpleRenderer;
                        if (renderer != null)
                        {
                            _originalSymbol = renderer.Symbol;
                        }

                        // Skapa transparent symbol med synliga gränser
                        var outlineSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(
                            ColorFactory.Instance.CreateRGBColor(0, 0, 0, 0),     
                            SimpleFillStyle.Solid
                        );

                        var simpleRenderer = new CIMSimpleRenderer
                        {
                            Symbol = outlineSymbol.MakeSymbolReference()
                        };

                        _kommunLayer.SetRenderer(simpleRenderer);
                        System.Diagnostics.Debug.WriteLine("Enkel transparens applicerad");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fel vid enkel transparens: {ex.Message}");
                }
            });
        }


        private async Task HighlightKommun(string kommunName, bool isCorrect)
        {
            await QueuedTask.Run(() =>
            {
                try
                {
                    if (_kommunLayer == null || string.IsNullOrEmpty(kommunName))
                        return;

                    CIMPolygonSymbol highlightSymbol;

                    if (isCorrect)
                    {   // Grön för rätt svar
                        highlightSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(
                            ColorFactory.Instance.CreateRGBColor(0, 250, 0, 80),
                            SimpleFillStyle.Solid
                        );
                    }
                    else
                    {   // Röd för fel svar
                        highlightSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(
                            ColorFactory.Instance.CreateRGBColor(150, 0, 0),
                            SimpleFillStyle.Solid
                        );
                    }
                    // Deff query för att visa den specifika kommunen
                    var definitionQuery = $"NAMN = '{kommunName}'";

                    // Isolera deff query för spec kommun
                    _kommunLayer.SetDefinitionQuery(definitionQuery);


                    // Highlight symbol
                    var highlightRenderer = new CIMSimpleRenderer()
                    {
                        Symbol = highlightSymbol.MakeSymbolReference()
                    };

                    _kommunLayer.SetRenderer(highlightRenderer);

                    _lastAnswersedKommunName = kommunName;

                    System.Diagnostics.Debug.WriteLine($"Highlight {kommunName}({(isCorrect ? "Rätt" : "Fel")})");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fel vid highlight{ex.Message}");
                }
            });
        }

        private async Task ResetKommunAppearance()
        {
            if (_kommunLayer == null) return;

            await QueuedTask.Run(() =>
            {
                try
                {
                    // Ta bort definition query för att visa alla kommuner igen
                    _kommunLayer.SetDefinitionQuery("");

                    // Återställ till transparent symbol
                    var transparentSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(
                        ColorFactory.Instance.CreateRGBColor(0, 0, 0, 0),
                        SimpleFillStyle.Solid
                    );

                    var transparentRenderer = new CIMSimpleRenderer
                    {
                        Symbol = transparentSymbol.MakeSymbolReference()
                    };

                    _kommunLayer.SetRenderer(transparentRenderer);
                    System.Diagnostics.Debug.WriteLine("Kommuner återställda till transparanta");

                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fel vid återställning: {ex.Message}");
                }
            });
        }

        private async Task VisaHistorikPost(SpelHistorik historikPost)
        {
            if (historikPost?.KorrektPlats == null) return;

            await QueuedTask.Run(() =>
            {
                try
                {
                    // Zooma till den korrekta platsen
                    var env = EnvelopeBuilder.CreateEnvelope(
                        historikPost.KorrektPlats.X - 50000,
                        historikPost.KorrektPlats.Y - 50000,
                        historikPost.KorrektPlats.X + 50000,
                        historikPost.KorrektPlats.Y + 50000,
                        historikPost.KorrektPlats.SpatialReference
                    );

                    MapView.Active.ZoomTo(env);

                    // Highlight den korrekta kommunen
                    if (!string.IsNullOrEmpty(historikPost.Kommun))
                    {
                        CIMPolygonSymbol highlightSymbol;

                        if (historikPost.VarRätt)
                        {
                            highlightSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(
                                ColorFactory.Instance.CreateRGBColor(0, 255, 0, 60), // Grön för rätt
                                SimpleFillStyle.Solid
                            );
                        }
                        else
                        {
                            highlightSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(
                                ColorFactory.Instance.CreateRGBColor(255, 0, 0, 60), // Röd för fel
                                SimpleFillStyle.Solid
                            );
                        }

                        var definitionQuery = $"NAMN = '{historikPost.Kommun}'";
                        _kommunLayer?.SetDefinitionQuery(definitionQuery);

                        var highlightRenderer = new CIMSimpleRenderer
                        {
                            Symbol = highlightSymbol.MakeSymbolReference()
                        };
                        _kommunLayer?.SetRenderer(highlightRenderer);
                    }

                    System.Diagnostics.Debug.WriteLine($"Visar historik: {historikPost.Kommun}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fel vid visning av historik: {ex.Message}");
                }
            });
        }

        internal async void OnShowOnMap(SpelHistorik historikItem)
        {
            if (historikItem?.KorrektPlats != null)
            {
                await QueuedTask.Run(() =>
                {
                    try
                    {
                        MapView.Active.ZoomTo(historikItem.KorrektPlats, TimeSpan.FromSeconds(0.3));


                        // Highlight kommunen
                        if (!string.IsNullOrEmpty(historikItem.Kommun))
                        {
                            CIMPolygonSymbol highlightSymbol;
                            if (historikItem.VarRätt)
                            {
                                highlightSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(
                                    ColorFactory.Instance.CreateRGBColor(0, 255, 0, 60),
                                    SimpleFillStyle.Solid
                                );
                            }
                            else
                            {
                                highlightSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(
                                    ColorFactory.Instance.CreateRGBColor(255, 0, 0, 60),
                                    SimpleFillStyle.Solid
                                );
                            }

                            var definitionQuery = $"NAMN = '{historikItem.Kommun}'";
                            _kommunLayer?.SetDefinitionQuery(definitionQuery);
                            var highlightRenderer = new CIMSimpleRenderer
                            {
                                Symbol = highlightSymbol.MakeSymbolReference()
                            };
                            _kommunLayer?.SetRenderer(highlightRenderer);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Fel vid visning på karta: {ex.Message}");
                    }
                });
            }
        }
        #endregion

        #region INotifyPropertyChanged & Hjälp
        public new event PropertyChangedEventHandler PropertyChanged;

        protected new void NotifyPropertyChanged([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected new bool SetProperty<T>(ref T field, T value, [CallerMemberName] string name = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            NotifyPropertyChanged(name);
            return true;
        }
        #endregion
    }
}
