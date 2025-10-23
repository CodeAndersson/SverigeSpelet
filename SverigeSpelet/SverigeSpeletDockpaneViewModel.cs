using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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
    public class SverigeSpeletDockpaneViewModel : DockPane, INotifyPropertyChanged
    {
        private const string _dockPaneID = "SverigeSpelet_SverigeSpeletDockpane";
        // Props
        private bool _showGameView;
        private bool _showSettingsView = true;
        private string _currentQuestion;
        private string _timeLeftText;
        private string _pointsText;
        private string _questionCountText;
        private string _resultText;
        private double _timeProgress;
        private string _playerName = "Gäst";
        private string _difficultyLevel = "Lätt";

        private int _questionIndex = 0;
        private int _totalPoints = 0;
        private int _maxTime = 10;
        private int _timeRemaining;
        private DispatcherTimer _timer;
        private List<SpelData> _currentQuestions = new();

        private Geometry _userGuessGeometry;
        private Geometry _correctAnswerGeometry;
        private Geometry _lineGeometry;
        private string _distanceText;
        private bool _showLine;


        // Egenskaper för binding
        private Geometry UserGuessGeometry
        {
            get => _userGuessGeometry;
            set => SetProperty(ref _userGuessGeometry, value);
        }
        private Geometry CorrectAnswerGeometry
        {
            get => _correctAnswerGeometry;
            set => SetProperty(ref _correctAnswerGeometry, value);
        }
        private Geometry LineGeometry
        {
            get => _lineGeometry;
            set => SetProperty(ref _lineGeometry, value);
        }
        private string DistanceText
        {
            get => _distanceText;
            set => SetProperty(ref _distanceText, value);
        }
        private bool ShowLine
        {
            get => _showLine;
            set => SetProperty(ref _showLine, value);
        }
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

        public string PointsText
        {
            get => _pointsText;
            set => SetProperty(ref _pointsText, value);
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

        public List<SpelResultat> TopList { get; set; } = new();

        // Commands
        public ICommand StartGameCommand { get; }
        public ICommand EndGameCommand { get; }
        public ICommand UpdateTopListCommand { get; }

        // Konstruktor
        protected SverigeSpeletDockpaneViewModel() : base()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;

            StartGameCommand = new RelayCommand(async () => await StartaSpel());
            EndGameCommand = new RelayCommand(async () => await AvslutaSpel());
            UpdateTopListCommand = new RelayCommand(() => UppdateraTopplista());

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
                _questionIndex = 0;
                _totalPoints = 0;
                _currentQuestions.Clear();

                ShowSettingsView = false;
                ShowGameView = true;

                _playerName = "Gäst";
                _difficultyLevel = "Lätt";

                await FrameworkApplication.SetCurrentToolAsync("SverigeSpelet_SverigeSpeletMapTool");
                await InitieraSpel();

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
                await AvslutaSpel();
                return;
            }

            var q = _currentQuestions[_questionIndex];
            CurrentQuestion = $"Var ligger {q.Namn}?";
            QuestionCountText = $"Fråga {_questionIndex + 1} av {_currentQuestions.Count}";
            PointsText = $"Totalpoäng: {_totalPoints}";
            ResultText = "";

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

            _timeRemaining = _maxTime;
            TimeProgress = 100;
            TimeLeftText = $"{_timeRemaining} sekunder kvar";
            _timer.Start();
        }

        private async void Timer_Tick(object sender, EventArgs e)
        {
            _timeRemaining--;
            TimeProgress = (_timeRemaining / (double)_maxTime) * 100;
            TimeLeftText = $"{_timeRemaining} sekunder kvar";

            if (_timeRemaining <= 0)
            {
                _timer.Stop();
                await HanteraSvarAsync(false, 0);
            }
        }

        private async Task HanteraSvarAsync(bool correct, double distance)
        {
            _timer.Stop();

            int points = 0;
            if (correct)
            {
                points = CalculatedPoints(_timeRemaining, _difficultyLevel);
                ResultText = $"Rätt! +{points} poäng";
            }
            else
            {
                ResultText = distance > 0 ? $"Fel! Du var {distance:F0} m bort" : "Fel!";
            }

            _totalPoints += points;
            PointsText = $"Totalpoäng: {_totalPoints}";

            await Task.Delay(1500);
            _questionIndex++;
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
            _timer.Stop();
            await FrameworkApplication.SetCurrentToolAsync(null);

            SparaResultat();
            UppdateraTopplista();
            ResultText = $"Spelet avslutat! Slutpoäng: {_totalPoints}";
            
            await Task.Delay(2000);
            ShowGameView = false;
            ShowSettingsView = true;
        }

        #endregion

        #region === Kartinteraktion ===

        internal async void HanteraKartKlick(MapPoint klick)
        {
            if (!_showGameView || _questionIndex >= _currentQuestions.Count)
                return;

            var current = _currentQuestions[_questionIndex];
            var dist = CalculateDistance(klick, current.Geometri);
            var isRight = dist < GetTolerans(_difficultyLevel);

            await HanteraSvarAsync(isRight, dist);
        }

        private double CalculateDistance(MapPoint clicked, Geometry geom)
        {
            if (clicked == null || geom == null) return double.MaxValue;
            var center = geom is Polygon p ? GeometryEngine.Instance.Centroid(p) as MapPoint : geom as MapPoint;
            return center == null ? double.MaxValue : GeometryEngine.Instance.Distance(clicked, center);
        }

        private double GetTolerans(string diff) => diff switch
        {
            "Lätt" => 50000,
            "Medel" => 30000,
            "Svår" => 10000,
            _ => 30000
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

        private void UpdateScoreBasedOnDistance(double distance)
        {
            int points = (int)(100 / (distance + 1));
            _totalPoints += Math.Max(points, 1);
            PointsText = $"Totalpoäng: { _totalPoints }";
        }

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
                string db = @"C:\Quiz.gdb";
                using var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(db)));
                var fc = gdb.OpenDataset<FeatureClass>("Kommun");
                return LayerFactory.Instance.CreateLayer<FeatureLayer>(
                    new FeatureLayerCreationParams(fc), MapView.Active.Map);
            });
        }

        private async Task<List<SpelData>> SkapaSpelData(FeatureLayer layer)
        {
            return await QueuedTask.Run(() =>
            {
                var list = new List<SpelData>();
                var fc = layer.GetFeatureClass();
                using var cursor = fc.Search(new QueryFilter { WhereClause = "1=1" });
                while (cursor.MoveNext())
                {
                    using var f = (Feature)cursor.Current;
                    var geom = f.GetShape();
                    var namn = f["NAMN"]?.ToString();
                    if (geom != null && !string.IsNullOrEmpty(namn))
                        list.Add(new SpelData { Namn = namn, Geometri = geom });
                }
                return list;
            });
        }

        private List<SpelData> BlandaOchValjFragor(List<SpelData> alla, int antal)
        {
            var rnd = new Random();
            return alla.OrderBy(_ => rnd.Next()).Take(antal).ToList();
        }

        #endregion

        #region  INotifyPropertyChanged & Hjälp 
        public event PropertyChangedEventHandler PropertyChanged;

        protected new void NotifyPropertyChanged([CallerMemberName] string name = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

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

    // --- RelayCommand och hjälptyper ---
    public class RelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public RelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            _executeAsync = executeAsync;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public async void Execute(object parameter)
        {
            if (_execute != null) _execute();
            else if (_executeAsync != null) await _executeAsync();
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
