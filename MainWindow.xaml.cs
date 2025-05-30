using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using FlexibleAutomationSystem.Models;
using FlexibleAutomationSystem.Services;
using Microsoft.Win32;
using DataGridTextColumn = System.Windows.Controls.DataGridTextColumn;

namespace FlexibleAutomationSystem
{
    public partial class MainWindow : Window
    {
        private readonly DataManager _dataManager;
        private readonly GanttDiagramService _ganttService;
        private readonly PetriNetService _petriNetService;
        private readonly DataValidator _validator;

        private SystemData _systemData;
        private GanttDiagram _ganttDiagram;
        private PetriNetwork _petriNetwork;

        public MainWindow()
        {
            InitializeComponent();

            _dataManager = new DataManager();
            _ganttService = new GanttDiagramService();
            _petriNetService = new PetriNetService();
            _validator = new DataValidator();
            _systemData = new SystemData();

            InitializeUI();
            SetupEventHandlers();
        }

        private void InitializeUI()
        {
            InitializeDetailGrids();
            InitializeProductionGrid();
            InitializeTransportGrid();
            UpdateDataGrids();
        }

        private void SetupEventHandlers()
        {
            rbInputData.Checked += (_, __) => ShowPanel(gridInputData);
            rbGanttChart.Checked += (_, __) => ShowPanel(gridGantt);
            rbPetriNet.Checked += (_, __) => ShowPanel(gridPetri);

            canvasGantt.MouseLeftButtonDown += CanvasGantt_MouseLeftButtonDown;
            canvasGantt.MouseMove += CanvasGantt_MouseMove;
        }

        private void ShowPanel(Grid panel)
        {
            gridInputData.Visibility = Visibility.Collapsed;
            gridGantt.Visibility = Visibility.Collapsed;
            gridPetri.Visibility = Visibility.Collapsed;
            panel.Visibility = Visibility.Visible;
        }

        private static readonly Regex OnlyDigits = new(@"[^0-9]+");
        private void NumberValidationTextBox(object s, TextCompositionEventArgs e) =>
            e.Handled = OnlyDigits.IsMatch(e.Text);

        private static int GetInt(TextBox box, int @default) =>
            int.TryParse(box.Text, out var v) ? v : @default;


        private void InitializeDetailGrids()
        {
            dgDetailSteps.ItemsSource = null;
            dgDetailSteps.Columns.Clear();
            dgDetailStepTimes.ItemsSource = null;
            dgDetailStepTimes.Columns.Clear();
        }

        private void InitializeProductionGrid() => UpdateProductionGrid();
        private void InitializeTransportGrid() => UpdateTransportDistanceGrid();

        private void UpdateDetailGrids()
        {
            int details = GetInt(nudDetails, 14);
            int steps = GetInt(nudSteps, 5);

            dgDetailSteps.Columns.Clear();
            dgDetailStepTimes.Columns.Clear();

            for (int j = 0; j < steps; j++)
            {
                string path = $"[{j}]";

                dgDetailSteps.Columns.Add(new DataGridTextColumn
                {
                    Header = (j + 1).ToString(),
                    Binding = new System.Windows.Data.Binding(path),
                    Width = 50
                });

                dgDetailStepTimes.Columns.Add(new DataGridTextColumn
                {
                    Header = (j + 1).ToString(),
                    Binding = new System.Windows.Data.Binding(path),
                    Width = 50
                });
            }

            dgDetailSteps.ItemsSource = Enumerable.Range(0, details)
                .Select(i => new DetailRow
                {
                    RowHeader = $"Д{i + 1}",
                    Values = Enumerable.Repeat("0", steps).ToArray()
                }).ToList();

            dgDetailStepTimes.ItemsSource = Enumerable.Range(0, details)
                .Select(i => new DetailRow
                {
                    RowHeader = $"Д{i + 1}",
                    Values = Enumerable.Repeat("0", steps).ToArray()
                }).ToList();
        }

        private void UpdateProductionGrid()
        {
            int prod = GetInt(nudProduction, 5);
            var rows = dgProdCellCount.ItemsSource as List<CellCountData> ?? new();

            while (rows.Count < prod) rows.Add(new CellCountData { Value = "2" });
            while (rows.Count > prod) rows.RemoveAt(rows.Count - 1);

            dgProdCellCount.ItemsSource = null;
            dgProdCellCount.ItemsSource = rows;
        }

        private void UpdateTransportDistanceGrid()
        {
            int prod = GetInt(nudProduction, 5);
            int size = prod + 1;

            dgTransDistance.Columns.Clear();
            dgTransDistance.Columns.Add(new DataGridTextColumn
            {
                Header = "АС",
                Binding = new System.Windows.Data.Binding("[0]"),
                Width = 50
            });

            for (int i = 0; i < prod; i++)
            {
                dgTransDistance.Columns.Add(new DataGridTextColumn
                {
                    Header = $"М{i + 1}",
                    Binding = new System.Windows.Data.Binding($"[{i + 1}]"),
                    Width = 50
                });
            }

            var data = new List<TransportRow>
            {
                new() { RowHeader = "АС", Values = Enumerable.Repeat("0", size).ToArray() }
            };

            data.AddRange(Enumerable.Range(0, prod).Select(i => new TransportRow
            {
                RowHeader = $"М{i + 1}",
                Values = Enumerable.Repeat("0", size).ToArray()
            }));

            dgTransDistance.ItemsSource = data;
        }

        private void UpdateDataGrids()
        {
            UpdateDetailGrids();
            UpdateProductionGrid();
            UpdateTransportDistanceGrid();
        }


        private void NudDetails_ValueChanged(object s, TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            UpdateDataGrids();
            CheckDataErrors();
        }

        private void NudPetri_ValueChanged(object s, TextChangedEventArgs e)
        {
            if (_petriNetwork != null && IsLoaded)
                DrawPetriNet();
        }


        private void TxtTransport_TextChanged(object s, TextChangedEventArgs e) =>
            CheckDataErrors();

        private void CheckDataErrors()
        {
            try
            {
                _ = double.Parse(txtSpeed.Text);
                _ = double.Parse(txtTimeLoad.Text);
                _ = double.Parse(txtTimeUnload.Text);
                _ = double.Parse(txtTimeGiveTake.Text);

                ValidateGridData();
                lblDataError.Visibility = Visibility.Collapsed;
            }
            catch
            {
                lblDataError.Visibility = Visibility.Visible;
            }
        }

        private void ValidateGridData()
        {
            void ParseAll(IEnumerable<string> src, Action<string> p)
            {
                foreach (var s in src) p(s);
            }

            if (dgDetailSteps.ItemsSource is List<DetailRow> ds)
                ParseAll(ds.SelectMany(r => r.Values), s => int.Parse(s));

            if (dgDetailStepTimes.ItemsSource is List<DetailRow> dt)
                ParseAll(dt.SelectMany(r => r.Values), s => double.Parse(s));

            if (dgTransDistance.ItemsSource is List<TransportRow> tr)
                ParseAll(tr.SelectMany(r => r.Values), s => double.Parse(s));

            if (dgProdCellCount.ItemsSource is List<CellCountData> pc)
                ParseAll(pc.Select(x => x.Value), s => int.Parse(s));
        }


        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    DefaultExt = "txt"
                };
                if (dlg.ShowDialog() != true) return;

                CollectDataFromUI();
                _dataManager.SaveData(_systemData, dlg.FileName);

                MessageBox.Show("Дані успішно збережено!",
                                "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при збереженні: {ex.Message}",
                                "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    DefaultExt = "txt"
                };
                if (dlg.ShowDialog() != true) return;

                _systemData = _dataManager.LoadData(dlg.FileName);
                LoadDataToUI();

                MessageBox.Show("Дані успішно завантажено!",
                                "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при завантаженні: {ex.Message}",
                                "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CollectDataFromUI()
        {
            _systemData.NumDetails = GetInt(nudDetails, 14);
            _systemData.NumSteps = GetInt(nudSteps, 5);
            _systemData.NumProduction = GetInt(nudProduction, 5);
            _systemData.NumTransport = GetInt(nudTransport, 3);

            _systemData.AverageSpeed = double.Parse(txtSpeed.Text);
            _systemData.TimeLoad = double.Parse(txtTimeLoad.Text);
            _systemData.TimeUnload = double.Parse(txtTimeUnload.Text);
            _systemData.TimeGiveTake = double.Parse(txtTimeGiveTake.Text);

            CollectDetailSteps();
            CollectDetailStepTimes();
            CollectTransportDistances();
            CollectProductionCells();
        }

        private void CollectDetailSteps()
        {
            _systemData.DetailSteps = new int[
                _systemData.NumDetails, _systemData.NumSteps];

            if (dgDetailSteps.ItemsSource is not List<DetailRow> src) return;

            for (int i = 0; i < src.Count && i < _systemData.NumDetails; i++)
                for (int j = 0; j < src[i].Values.Length &&
                                j < _systemData.NumSteps; j++)
                    _systemData.DetailSteps[i, j] = int.Parse(src[i].Values[j]);
        }

        private void CollectDetailStepTimes()
        {
            _systemData.DetailStepTimes = new double[
                _systemData.NumDetails, _systemData.NumSteps];

            if (dgDetailStepTimes.ItemsSource is not List<DetailRow> src) return;

            for (int i = 0; i < src.Count && i < _systemData.NumDetails; i++)
                for (int j = 0; j < src[i].Values.Length &&
                                j < _systemData.NumSteps; j++)
                    _systemData.DetailStepTimes[i, j] = double.Parse(src[i].Values[j]);
        }

        private void CollectTransportDistances()
        {
            int size = _systemData.NumProduction + 1;
            _systemData.TransportDistances = new double[size, size];

            if (dgTransDistance.ItemsSource is not List<TransportRow> src) return;

            for (int i = 0; i < src.Count && i < size; i++)
                for (int j = 0; j < src[i].Values.Length && j < size; j++)
                    _systemData.TransportDistances[i, j] = double.Parse(src[i].Values[j]);

            _systemData.CalculateTransportTimes();
        }

        private void CollectProductionCells()
        {
            _systemData.ProductionCellCount =
                new int[_systemData.NumProduction];

            if (dgProdCellCount.ItemsSource is not List<CellCountData> src) return;

            for (int i = 0; i < src.Count && i < _systemData.NumProduction; i++)
                _systemData.ProductionCellCount[i] = int.Parse(src[i].Value);
        }

        private void LoadDataToUI()
        {
            nudDetails.Text = _systemData.NumDetails.ToString();
            nudSteps.Text = _systemData.NumSteps.ToString();
            nudProduction.Text = _systemData.NumProduction.ToString();
            nudTransport.Text = _systemData.NumTransport.ToString();

            txtSpeed.Text = _systemData.AverageSpeed.ToString();
            txtTimeLoad.Text = _systemData.TimeLoad.ToString();
            txtTimeUnload.Text = _systemData.TimeUnload.ToString();
            txtTimeGiveTake.Text = _systemData.TimeGiveTake.ToString();

            UpdateDataGrids();
            LoadDetailSteps();
            LoadDetailStepTimes();
            LoadTransportDistances();
            LoadProductionCells();
        }

        private void LoadDetailSteps()
        {
            if (dgDetailSteps.ItemsSource is not List<DetailRow> dst) return;

            for (int i = 0; i < dst.Count && i < _systemData.NumDetails; i++)
                for (int j = 0; j < dst[i].Values.Length &&
                                j < _systemData.NumSteps; j++)
                    dst[i].Values[j] = _systemData.DetailSteps[i, j].ToString();

            dgDetailSteps.Items.Refresh();
        }

        private void LoadDetailStepTimes()
        {
            if (dgDetailStepTimes.ItemsSource is not List<DetailRow> dst) return;

            for (int i = 0; i < dst.Count && i < _systemData.NumDetails; i++)
                for (int j = 0; j < dst[i].Values.Length &&
                                j < _systemData.NumSteps; j++)
                    dst[i].Values[j] = _systemData.DetailStepTimes[i, j].ToString();

            dgDetailStepTimes.Items.Refresh();
        }

        private void LoadTransportDistances()
        {
            if (dgTransDistance.ItemsSource is not List<TransportRow> dst) return;

            int size = _systemData.NumProduction + 1;
            for (int i = 0; i < dst.Count && i < size; i++)
                for (int j = 0; j < dst[i].Values.Length && j < size; j++)
                    dst[i].Values[j] = _systemData.TransportDistances[i, j].ToString();

            dgTransDistance.Items.Refresh();
        }

        private void LoadProductionCells()
        {
            if (dgProdCellCount.ItemsSource is not List<CellCountData> dst) return;

            for (int i = 0; i < dst.Count && i < _systemData.NumProduction; i++)
                dst[i].Value = _systemData.ProductionCellCount[i].ToString();

            dgProdCellCount.Items.Refresh();
        }

        private void BtnNext_Click(object s, RoutedEventArgs e) =>
            rbGanttChart.IsChecked = true;

        private void BtnCalculate_Click(object s, RoutedEventArgs e)
        {
            try
            {
                CollectDataFromUI();

                var prodRule = (ProductionPriorityRule)cmbProdRule.SelectedIndex;
                var transRule = (TransportPriorityRule)cmbTransRule.SelectedIndex;
                var type = chkWideGantt.IsChecked == true
                               ? DiagramType.Extended
                               : DiagramType.Standard;

                _ganttDiagram = _ganttService.Calculate(_systemData,
                                                        prodRule, transRule, type);

                DisplayGanttDiagram();
                DisplayWorkModules();
                txtCycleTime.Text = _ganttDiagram.CycleTime.ToString("F2");

                if (type == DiagramType.Extended)
                {
                    _petriNetwork = _petriNetService.GenerateNetwork(_ganttDiagram);
                    UpdatePetriNetTables();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при розрахунку: {ex.Message}",
                                "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayGanttDiagram()
        {
            canvasGantt.Children.Clear();
            new GanttDrawer(canvasGantt, _ganttDiagram)
                .Draw(chkDrawLegend.IsChecked == true);
        }

        private void DisplayWorkModules()
        {
            dgWorkModules.Columns.Clear();
            dgWorkModules.ItemsSource = null;

            int showTime = cmbShowTime.SelectedIndex;
            int maxCols = _ganttDiagram.ModuleWorks.Max(m => m.Count);

            for (int i = 0; i < maxCols; i++)
            {
                dgWorkModules.Columns.Add(new DataGridTextColumn
                {
                    Header = (i + 1).ToString(),
                    Binding = new System.Windows.Data.Binding($"[{i}]"),
                    Width = 80
                });
            }

            var rows = new List<WorkModuleRow>();

            for (int i = 0; i < _ganttDiagram.ModuleWorks.Length; i++)
            {
                var row = new WorkModuleRow
                {
                    Header = i < _systemData.NumProduction
                             ? $"ГВМ{i + 1}"
                             : $"АТМ{i - _systemData.NumProduction + 1}",
                    Values = new string[maxCols]
                };

                for (int j = 0; j < _ganttDiagram.ModuleWorks[i].Count; j++)
                {
                    var w = _ganttDiagram.ModuleWorks[i][j];
                    row.Values[j] = showTime switch
                    {
                        1 => $"{w.DetailIndex + 1} [{w.ProcessTime:F1}]",
                        2 => $"{w.DetailIndex + 1} [{w.StartTime:F1}-{w.EndTime:F1}]",
                        _ => (w.DetailIndex + 1).ToString()
                    };
                }
                rows.Add(row);
            }

            dgWorkModules.ItemsSource = rows;
        }

        private void UpdatePetriNetTables()
        {
            if (_petriNetwork == null) return;
            dgPetriProcesses.ItemsSource = _petriNetwork.Processes
                .Select(p => new { p.Name, p.Description }).ToList();

            var positionsList = new List<dynamic>();

            foreach (var proc in _petriNetwork.Processes)
            {
                foreach (var pos in proc.InputPositions)
                {
                    positionsList.Add(new
                    {
                        Process = proc.Name,
                        Name = pos.Name,
                        Description = pos.Description,
                        Type = "Вхідна"
                    });
                }

                foreach (var pos in proc.InhibitorPositions)
                {
                    positionsList.Add(new
                    {
                        Process = proc.Name,
                        Name = pos.Name + " (*)",
                        Description = pos.Description,
                        Type = "Інгібіторна"
                    });
                }

                foreach (var pos in proc.OutputPositions)
                {
                    positionsList.Add(new
                    {
                        Process = proc.Name,
                        Name = pos.Name,
                        Description = pos.Description,
                        Type = "Вихідна"
                    });
                }
            }

            dgPetriPositions.ItemsSource = positionsList;

            dgPetriStart.ItemsSource = _petriNetwork.InitialPositions
                .Select(p => new { p.Name, p.Description }).ToList();

            dgPetriFinish.ItemsSource = _petriNetwork.FinalPositions
                .Select(p => new { p.Name, p.Description }).ToList();

            DrawPetriNet();
        }


        private void DrawPetriNet()
        {
            int scale = GetInt(txtPetriScale, 100);
            int perLine = GetInt(txtPetriPosLine, 10);

            var bmp = _petriNetService.DrawNetwork(_petriNetwork, scale, perLine);
            imgPetri.Source = BitmapToImageSource(bmp);
        }

        private static BitmapImage BitmapToImageSource(System.Drawing.Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.StreamSource = ms;
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            return bi;
        }

        private void BtnSavePetri_Click(object s, RoutedEventArgs e)
        {
            if (imgPetri.Source == null) return;

            var dlg = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png|All files (*.*)|*.*",
                DefaultExt = "png"
            };
            if (dlg.ShowDialog() != true) return;

            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create((BitmapSource)imgPetri.Source));

            using var fs = new FileStream(dlg.FileName, FileMode.Create);
            enc.Save(fs);

            MessageBox.Show("Зображення успішно збережено!",
                            "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CanvasGantt_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2 || _ganttDiagram == null) return;

            new GanttDrawer(canvasGantt, _ganttDiagram).ToggleGrayMode();
            DisplayGanttDiagram();
        }

        private void CanvasGantt_MouseMove(object s, MouseEventArgs e)
        {
            if (chkDrawLegend.IsChecked == true || _ganttDiagram == null) return;

            var desc = new GanttDrawer(canvasGantt, _ganttDiagram)
                       .GetDescriptionAtPoint(e.GetPosition(canvasGantt));

            canvasGantt.ToolTip = string.IsNullOrEmpty(desc) ? null : desc;
        }

        private void BtnAbout_Click(object s, RoutedEventArgs e) =>
            new AboutWindow().ShowDialog();
    }

    public class DetailRow
    {
        public string RowHeader { get; set; }
        public string[] Values { get; set; }
        public string this[int i] { get => Values[i]; set => Values[i] = value; }
    }

    public class TransportRow : DetailRow { }
    public class WorkModuleRow : DetailRow { public string Header { get; set; } }
    public class CellCountData { public string Value { get; set; } }
}
