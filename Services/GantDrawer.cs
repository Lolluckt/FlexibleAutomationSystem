using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FlexibleAutomationSystem.Models;

namespace FlexibleAutomationSystem.Services
{
    public class GanttDrawer
    {
        private readonly Canvas _canvas;
        private readonly GanttDiagram _diagram;
        private readonly Dictionary<int, Brush> _detailBrushes;
        private bool _grayMode = false;

        public GanttDrawer(Canvas canvas, GanttDiagram diagram)
        {
            _canvas = canvas;
            _diagram = diagram;
            _detailBrushes = GenerateDetailColors();
        }

        public void Draw(bool showLegend)
        {
            _canvas.Children.Clear();

            if (!_diagram.IsCalculated)
            {
                DrawNotCalculatedMessage();
                return;
            }

            if (showLegend)
            {
                DrawLegend();
                return;
            }

            DrawDiagram();
        }

        public void ToggleGrayMode()
        {
            _grayMode = !_grayMode;
        }

        private void DrawNotCalculatedMessage()
        {
            var text = new TextBlock
            {
                Text = "Розрахунок не проведено",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Blue
            };

            Canvas.SetLeft(text, _canvas.ActualWidth / 2 - 100);
            Canvas.SetTop(text, _canvas.ActualHeight / 2 - 10);
            _canvas.Children.Add(text);
        }

        private void DrawLegend()
        {
            var title = new TextBlock
            {
                Text = "Позначення деталей",
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(title, 37);
            Canvas.SetTop(title, 15);
            _canvas.Children.Add(title);

            int detailCount = Math.Min(_diagram.SystemData.NumDetails, 14);
            for (int i = 0; i < detailCount; i++)
            {
                int col = i / 7;
                int row = i % 7;

                var rect = new Rectangle
                {
                    Width = 20,
                    Height = 11,
                    Fill = _detailBrushes[i],
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(rect, 30 + col * 120);
                Canvas.SetTop(rect, 47 + row * 25);
                _canvas.Children.Add(rect);

                var label = new TextBlock
                {
                    Text = $"Деталь №{i + 1}",
                    FontSize = 9
                };
                Canvas.SetLeft(label, 52 + col * 120);
                Canvas.SetTop(label, 45 + row * 25);
                _canvas.Children.Add(label);
            }
        }

        private void DrawDiagram()
        {
            double leftMargin = 55;
            double topMargin = 15;
            double rightMargin = 15;
            double bottomMargin = 55;

            double width = _canvas.ActualWidth;
            double height = _canvas.ActualHeight;

            double diagramWidth = width - leftMargin - rightMargin;
            double diagramHeight = height - topMargin - bottomMargin;

            var border = new Rectangle
            {
                Width = diagramWidth,
                Height = diagramHeight,
                Stroke = Brushes.Black,
                StrokeThickness = 3,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(border, leftMargin);
            Canvas.SetTop(border, topMargin);
            _canvas.Children.Add(border);
            double timeScale = diagramWidth / _diagram.CycleTime;
            int moduleCount = _diagram.Type == DiagramType.Standard
                               ? _diagram.SystemData.NumProduction
                               : _diagram.SystemData.NumProduction + _diagram.SystemData.NumTransport;

            double rowHeight = diagramHeight / (moduleCount + 1);
            DrawTimeScale(leftMargin, topMargin, rightMargin, bottomMargin, timeScale);
            for (int row = 0; row < moduleCount; row++)
            {
                int moduleIndex = moduleCount - row - 1;
                double y = topMargin + rowHeight * (row + 1);
                _canvas.Children.Add(new Line
                {
                    X1 = leftMargin,
                    X2 = leftMargin + diagramWidth,
                    Y1 = y,
                    Y2 = y,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                });

                string name = moduleIndex < _diagram.SystemData.NumProduction
                            ? $"ГВМ{moduleIndex + 1}"
                            : $"АТМ{moduleIndex - _diagram.SystemData.NumProduction + 1}";

                var lbl = new TextBlock { Text = name, FontSize = 10, FontWeight = FontWeights.Bold };
                Canvas.SetLeft(lbl, leftMargin - 40);
                Canvas.SetTop(lbl, y - 8);
                _canvas.Children.Add(lbl);
                bool gray = false;

                foreach (var w in _diagram.ModuleWorks[moduleIndex])
                {
                    if (w.ProcessTime <= 0 ||
                        double.IsInfinity(w.StartTime) || double.IsNaN(w.StartTime) ||
                        double.IsInfinity(w.EndTime) || double.IsNaN(w.EndTime)) continue;

                    double startX = leftMargin + w.StartTime * timeScale;
                    double endX = leftMargin + w.EndTime * timeScale;
                    double widthR = endX - startX;
                    if (widthR <= 0) continue;

                    var rect = new Rectangle
                    {
                        Width = widthR,
                        Height = 11,
                        Fill = _grayMode ? (gray ? Brushes.LightGray : Brushes.DimGray)
                                           : _detailBrushes[w.DetailIndex],
                        Stroke = Brushes.Black,
                        StrokeThickness = 1
                    };
                    Canvas.SetLeft(rect, startX);
                    Canvas.SetTop(rect, y - 5);
                    _canvas.Children.Add(rect);

                    if (_diagram.DrawDetails && widthR > 15)
                    {
                        var detLbl = new TextBlock { Text = (w.DetailIndex + 1).ToString(), FontSize = 6 };
                        Canvas.SetLeft(detLbl, startX + 2);
                        Canvas.SetTop(detLbl, y - 13);
                        _canvas.Children.Add(detLbl);
                    }

                    if (_grayMode) gray = !gray;
                }
            }
            Canvas.SetZIndex(border, int.MaxValue);
        }


        private void DrawTimeScale(double left, double top, double right, double bottom, double timeScale)
        {
            double width = _canvas.ActualWidth - left - right;
            double height = _canvas.ActualHeight - top - bottom;
            double timeStep = DetermineTimeStep(_diagram.CycleTime);
            double pixelStep = timeStep * timeScale;
            for (double time = 0; time <= _diagram.CycleTime; time += timeStep)
            {
                double x = left + time * timeScale;

                var line = new Line
                {
                    X1 = x,
                    Y1 = top,
                    X2 = x,
                    Y2 = top + height,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                };
                _canvas.Children.Add(line);

                var label = new TextBlock
                {
                    Text = time.ToString("F0"),
                    FontSize = 8
                };
                Canvas.SetLeft(label, x - 15);
                Canvas.SetTop(label, top + height + 7);
                _canvas.Children.Add(label);
            }
        }

        private double DetermineTimeStep(double totalTime)
        {
            double[] possibleSteps = { 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000, 5000 };

            foreach (var step in possibleSteps)
            {
                if (totalTime / step <= 15)
                {
                    return step;
                }
            }

            return 5000;
        }

        private Dictionary<int, Brush> GenerateDetailColors()
        {
            var brushes = new Dictionary<int, Brush>();
            var predefinedColors = new[]
            {
                Color.FromRgb(252, 0, 0),     // Червоний
                Color.FromRgb(0, 63, 0),      // Темно-зелений
                Color.FromRgb(189, 63, 0),    // Коричневий
                Color.FromRgb(252, 63, 0),    // Помаранчевий
                Color.FromRgb(0, 189, 0),     // Зелений
                Color.FromRgb(252, 189, 0),   // Жовтий
                Color.FromRgb(126, 252, 0),   // Салатовий
                Color.FromRgb(252, 252, 0),   // Яскраво-жовтий
                Color.FromRgb(63, 0, 63),     // Фіолетовий
                Color.FromRgb(127, 127, 127), // Сірий
                Color.FromRgb(0, 0, 189),     // Синій
                Color.FromRgb(252, 0, 189),   // Рожевий
                Color.FromRgb(0, 127, 189),   // Блакитний
                Color.FromRgb(0, 189, 252),   // Світло-блакитний
                Color.FromRgb(63, 63, 63)     // Темно-сірий
            };

            for (int i = 0; i < _diagram.SystemData.NumDetails; i++)
            {
                if (i < predefinedColors.Length)
                {
                    brushes[i] = new SolidColorBrush(predefinedColors[i]);
                }
                else
                {
                    int r = (i * 37 + 100) % 256;
                    int g = (i * 67 + 50) % 256;
                    int b = (i * 97 + 150) % 256;
                    brushes[i] = new SolidColorBrush(Color.FromRgb((byte)r, (byte)g, (byte)b));
                }
            }

            return brushes;
        }

        public string GetDescriptionAtPoint(Point point)
        {
            double leftMargin = 55;
            double topMargin = 15;
            double rightMargin = 15;
            double bottomMargin = 55;

            double width = _canvas.ActualWidth;
            double height = _canvas.ActualHeight;

            double diagramWidth = width - leftMargin - rightMargin;
            double diagramHeight = height - topMargin - bottomMargin;

            double timeScale = diagramWidth / _diagram.CycleTime;
            int moduleCount = _diagram.Type == DiagramType.Standard ?
                            _diagram.SystemData.NumProduction :
                            _diagram.SystemData.NumProduction + _diagram.SystemData.NumTransport;

            double moduleHeight = diagramHeight / (moduleCount + 1);
            if (point.X < leftMargin || point.X > width - rightMargin ||
                point.Y < topMargin || point.Y > height - bottomMargin)
            {
                return null;
            }

            int moduleIndex = (int)((point.Y - topMargin) / moduleHeight) - 1;
            if (moduleIndex < 0 || moduleIndex >= moduleCount)
            {
                return null;
            }

            moduleIndex = moduleCount - moduleIndex - 1;
            double time = (point.X - leftMargin) / timeScale;


            foreach (var work in _diagram.ModuleWorks[moduleIndex])
            {
                if (time >= work.StartTime && time <= work.EndTime &&
                    Math.Abs(point.Y - (topMargin + moduleHeight * (moduleCount - moduleIndex))) < 5)
                {
                    return work.Description;
                }
            }

            return null;
        }
    }
}