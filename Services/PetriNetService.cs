using System.Drawing;
using System.Drawing.Drawing2D;
using FlexibleAutomationSystem.Models;

namespace FlexibleAutomationSystem.Services
{
    public class PetriNetService
    {
        private PetriNetwork _network;
        private Dictionary<string, PetriPosition> _positionCache;

        public PetriNetwork GenerateNetwork(GanttDiagram diagram)
        {
            _network = new PetriNetwork(diagram);
            _positionCache = new Dictionary<string, PetriPosition>();

            GenerateProcesses();
            GenerateAllDetailPositions();
            GenerateModulePositions();
            // GenerateInitialAndFinalPositions(); 
            // CollectAllPositions(); 

            return _network;
        }

        private void GenerateProcesses()
        {
            int processNumber = 1;

            for (int module = 0; module < _network.SourceDiagram.ModuleWorks.Length; module++)
            {
                _network.ModuleProcesses[module].Clear();

                foreach (var work in _network.SourceDiagram.ModuleWorks[module])
                {
                    var process = new PetriProcess(
                        $"Т{processNumber}",
                        work.Description,
                        module,
                        work.DetailIndex,
                        work.OperationIndex
                    );

                    if (work.FromModule != 0 || work.ToModule != 0)
                    {
                        process.ModuleFrom = work.FromModule;
                        process.ModuleTo = work.ToModule;
                    }

                    _network.ModuleProcesses[module].Add(process);
                    _network.Processes.Add(process);
                    processNumber++;
                }
            }
        }

        private void GenerateAllDetailPositions()
        {
            var diagram = _network.SourceDiagram;
            var data = diagram.SystemData;

            for (int detail = 0; detail < data.NumDetails; detail++)
            {
                _network.DetailPositions[detail].Clear();
                int detailPosNumber = 1;

                var initialPos = new PetriPosition(
                    $"Д{detail + 1}_{detailPosNumber}",
                    $"Д{detail + 1} на АС перед 1 операцією",
                    PositionClass.Detail, CollectorType.Exit, 0, detail, 0
                );
                _network.DetailPositions[detail].Add(initialPos);
                _positionCache[GetPositionKey(detail, 0, 0, CollectorType.Exit)] = initialPos;
                detailPosNumber++;

                for (int op = 0; op < diagram.DetailProcesses[detail].Count; op++)
                {
                    var process = diagram.DetailProcesses[detail][op];
                    int moduleIndex = process.ModuleIndex;

                    var entryPos = new PetriPosition(
                        $"Д{detail + 1}_{detailPosNumber}",
                        $"Д{detail + 1} у {(data.ProductionCellCount[moduleIndex - 1] == 1 ? "" : "вхідному ")}накопичувачі ГВМ{moduleIndex} (перед {op + 1} операцією)",
                        PositionClass.Detail, CollectorType.Entry, moduleIndex, detail, op
                    );
                    _network.DetailPositions[detail].Add(entryPos);
                    _positionCache[GetPositionKey(detail, moduleIndex, op, CollectorType.Entry)] = entryPos;
                    detailPosNumber++;

                    var exitPos = new PetriPosition(
                        $"Д{detail + 1}_{detailPosNumber}",
                        $"Д{detail + 1} у {(data.ProductionCellCount[moduleIndex - 1] == 1 ? "" : "вихідному ")}накопичувачі ГВМ{moduleIndex} (після {op + 1} операції)",
                        PositionClass.Detail, CollectorType.Exit, moduleIndex, detail, op
                    );
                    _network.DetailPositions[detail].Add(exitPos);
                    _positionCache[GetPositionKey(detail, moduleIndex, op, CollectorType.Exit)] = exitPos;
                    detailPosNumber++;
                }

                var finalPos = new PetriPosition(
                    $"Д{detail + 1}_{detailPosNumber}",
                    $"Д{detail + 1} на АС після закінчення обробки",
                    PositionClass.Detail, CollectorType.Exit, 0, detail, diagram.DetailProcesses[detail].Count
                );
                _network.DetailPositions[detail].Add(finalPos);
                _positionCache[GetPositionKey(detail, 0, diagram.DetailProcesses[detail].Count, CollectorType.Exit)] = finalPos;
            }
        }
        private void GenerateModulePositions()
        {
            var data = _network.SourceDiagram.SystemData;

            for (int prod = 0; prod < data.NumProduction; prod++)
            {
                _network.ProductionPositions[prod].Clear();
                for (int i = 0; i < _network.ModuleProcesses[prod].Count; i++)
                {
                    var work = _network.SourceDiagram.ModuleWorks[prod][i];
                    var readyPos = new PetriPosition(
                        $"Р{prod + 1}_{i + 1}",
                        $"ГВМ{prod + 1} готовий обробити Д{work.DetailIndex + 1} (на {work.OperationIndex + 1} операції)",
                        PositionClass.Production, prod + 1, work.DetailIndex, work.OperationIndex
                    );
                    _network.ProductionPositions[prod].Add(readyPos);
                }

                for (int i = 0; i < _network.ModuleProcesses[prod].Count; i++)
                {
                    var process = _network.ModuleProcesses[prod][i];
                    var work = _network.SourceDiagram.ModuleWorks[prod][i];

                    process.InputPositions.Add(_network.ProductionPositions[prod][i]);

                    if (i > 0)
                    {
                        var prevWork = _network.SourceDiagram.ModuleWorks[prod][i - 1];
                        if (data.ProductionCellCount[prod] == 2)
                        {
                            var inhibitorKey = GetPositionKey(prevWork.DetailIndex, prod + 1, prevWork.OperationIndex, CollectorType.Exit);
                            if (_positionCache.TryGetValue(inhibitorKey, out var inhibitorPos))
                                process.InhibitorPositions.Add(inhibitorPos);
                        }
                        else if (data.ProductionCellCount[prod] == 1)
                        {
                            var inhibitorEntryKey = GetPositionKey(prevWork.DetailIndex, prod + 1, prevWork.OperationIndex, CollectorType.Entry);
                            if (_positionCache.TryGetValue(inhibitorEntryKey, out var inhibitorEntryPos))
                                process.InhibitorPositions.Add(inhibitorEntryPos);
                            var inhibitorExitKey = GetPositionKey(prevWork.DetailIndex, prod + 1, prevWork.OperationIndex, CollectorType.Exit);
                            if (_positionCache.TryGetValue(inhibitorExitKey, out var inhibitorExitPos))
                                process.InhibitorPositions.Add(inhibitorExitPos);
                        }
                    }
                    else if (data.ProductionCellCount[prod] == 1 && _network.ModuleProcesses[prod].Count > 1)
                    {
                        var lastWork = _network.SourceDiagram.ModuleWorks[prod].Last();
                        var inhibitorEntryKey = GetPositionKey(lastWork.DetailIndex, prod + 1, lastWork.OperationIndex, CollectorType.Entry);
                        if (_positionCache.TryGetValue(inhibitorEntryKey, out var inhibitorEntryPos))
                            process.InhibitorPositions.Add(inhibitorEntryPos);
                        var inhibitorExitKey = GetPositionKey(lastWork.DetailIndex, prod + 1, lastWork.OperationIndex, CollectorType.Exit);
                        if (_positionCache.TryGetValue(inhibitorExitKey, out var inhibitorExitPos))
                            process.InhibitorPositions.Add(inhibitorExitPos);
                    }

                    var detailEntryKey = GetPositionKey(work.DetailIndex, prod + 1, work.OperationIndex, CollectorType.Entry);
                    if (_positionCache.TryGetValue(detailEntryKey, out var detailEntryPos))
                        process.InputPositions.Add(detailEntryPos);

                    var detailExitKey = GetPositionKey(work.DetailIndex, prod + 1, work.OperationIndex, CollectorType.Exit);
                    if (_positionCache.TryGetValue(detailExitKey, out var detailExitPos))
                        process.OutputPositions.Add(detailExitPos);

                    if (i == _network.ModuleProcesses[prod].Count - 1)
                        process.OutputPositions.Add(_network.ProductionPositions[prod][0]);
                    else
                        process.OutputPositions.Add(_network.ProductionPositions[prod][i + 1]);
                }
            }

            for (int trans = 0; trans < data.NumTransport; trans++)
            {
                int moduleIndex = data.NumProduction + trans;
                _network.TransportPositions[trans].Clear();

                for (int i = 0; i < _network.ModuleProcesses[moduleIndex].Count; i++)
                {
                    var work = _network.SourceDiagram.ModuleWorks[moduleIndex][i];
                    string fromStr = work.FromModule == 0 ? "АС" : $"ГВМ{work.FromModule}";
                    string toStr = work.ToModule == 0 ? "АС" : $"ГВМ{work.ToModule}";
                    var readyPos = new PetriPosition(
                        $"А{trans + 1}_{i + 1}",
                        $"АТМ{trans + 1} готовий транспортувати Д{work.DetailIndex + 1} з {fromStr} на {toStr}",
                        PositionClass.Transport, moduleIndex + 1, work.DetailIndex, work.OperationIndex, work.FromModule, work.ToModule
                    );
                    _network.TransportPositions[trans].Add(readyPos);
                }

                for (int i = 0; i < _network.ModuleProcesses[moduleIndex].Count; i++)
                {
                    var process = _network.ModuleProcesses[moduleIndex][i];
                    var work = _network.SourceDiagram.ModuleWorks[moduleIndex][i];
                    process.InputPositions.Add(_network.TransportPositions[trans][i]);
                    int detailOpIndexForEntryToModule = work.OperationIndex; 
                    int detailOpIndexForExitFromModule = work.OperationIndex - 1; 

                    PetriPosition detailFromPos = null;
                    if (work.FromModule == 0)
                    {
                        var key = GetPositionKey(work.DetailIndex, 0, 0, CollectorType.Exit);
                        _positionCache.TryGetValue(key, out detailFromPos);
                    }
                    else
                    {
                        var key = GetPositionKey(work.DetailIndex, work.FromModule, detailOpIndexForExitFromModule, CollectorType.Exit);
                        _positionCache.TryGetValue(key, out detailFromPos);
                    }
                    if (detailFromPos != null) process.InputPositions.Add(detailFromPos);

                    PetriPosition detailToPos = null;
                    if (work.ToModule == 0) // На АС
                    {
                        var key = GetPositionKey(work.DetailIndex, 0, _network.SourceDiagram.DetailProcesses[work.DetailIndex].Count, CollectorType.Exit);
                        _positionCache.TryGetValue(key, out detailToPos);
                    }
                    else
                    {
                        var key = GetPositionKey(work.DetailIndex, work.ToModule, detailOpIndexForEntryToModule, CollectorType.Entry);
                        _positionCache.TryGetValue(key, out detailToPos);
                    }
                    if (detailToPos != null) process.OutputPositions.Add(detailToPos);


                    if (i == _network.ModuleProcesses[moduleIndex].Count - 1)
                        process.OutputPositions.Add(_network.TransportPositions[trans][0]);
                    else
                        process.OutputPositions.Add(_network.TransportPositions[trans][i + 1]);
                }
            }
            CollectAllPositions();
        }


        private string GetPositionKey(int detail, int module, int operation, CollectorType collector)
        {
            return $"D{detail}_M{module}_O{operation}_C{collector}";
        }

        private void CollectAllPositions()
        {
            _network.Positions.Clear();
            var addedPositions = new HashSet<PetriPosition>();

            foreach (var process in _network.Processes)
            {
                var allProcessPositions = process.InputPositions
                    .Concat(process.OutputPositions)
                    .Concat(process.InhibitorPositions);

                foreach (var pos in allProcessPositions)
                {
                    if (addedPositions.Add(pos))
                    {
                        _network.Positions.Add(pos);
                    }
                }
            }
        }


        private int GetMaxPositionsCount(PetriNetwork network)
        {
            int maxCount = 1;
            if (network.Processes == null || !network.Processes.Any()) return maxCount;

            foreach (var process in network.Processes)
            {
                if (process == null) continue;
                int currentProcessMax = Math.Max(
                    (process.InputPositions?.Count ?? 0) + (process.InhibitorPositions?.Count ?? 0),
                    process.OutputPositions?.Count ?? 0
                );
                if (currentProcessMax > maxCount)
                {
                    maxCount = currentProcessMax;
                }
            }
            return maxCount > 0 ? maxCount : 1;
        }


        private float CalcShift(int index, float circleSize, float circleDistance)
        {
            return index * (circleSize + circleDistance);
        }

        public Bitmap DrawNetwork(PetriNetwork network, int scale, int positionsPerLine)
        {
            float circleSize = 50f * scale / 100f;
            float circleDistance = 10f * scale / 100f;
            //float lineHeight = circleSize * GetMaxPositionsCount(network) + circleDistance * (GetMaxPositionsCount(network) -1);
            float lineDistance = 35f * scale / 100f;
            float horizontalCircleDistance = 40f * scale / 100f;
            float processHorizontalSize = circleSize + horizontalCircleDistance + 3f + horizontalCircleDistance + circleSize;
            float processHorizontalDistance = 50f * scale / 100f;
            float arrowHorizontalLength = 8f * scale / 100f;
            float arrowVerticalLength = 3f * scale / 100f;
            float fontCircleSize = Math.Max(6f, 8f * scale / 100f);
            float fontLineSize = Math.Max(8f, 10f * scale / 100f);
            float fontTitleSize = Math.Max(10f, 14f * scale / 100f);
            float titleLength = 120f * scale / 100f;

            Font fontCircle = new Font(FontFamily.GenericSansSerif, fontCircleSize, FontStyle.Bold);
            Font fontLine = new Font(FontFamily.GenericSansSerif, fontLineSize, FontStyle.Bold);
            Font fontTitle = new Font(FontFamily.GenericSansSerif, fontTitleSize, FontStyle.Bold);
            Pen blackPen = new Pen(Brushes.Black, 2f);

            var positionCoords = new Dictionary<PetriPosition, PointF>();
            int maxSlotsForAnyProcess = GetMaxPositionsCount(network);
            float dynamicLineHeight = maxSlotsForAnyProcess * circleSize + Math.Max(0, maxSlotsForAnyProcess - 1) * circleDistance;


            int totalProcesses = network.Processes.Count;
            if (totalProcesses == 0 && network.ModuleProcesses.All(mp => mp.Count == 0)) return new Bitmap(100, 100);

            int linesCount = 0;
            int tempCounter = 0;
            foreach (var moduleProcList in network.ModuleProcesses)
            {
                if (moduleProcList.Count > 0)
                {
                    linesCount += (int)Math.Ceiling((double)moduleProcList.Count / positionsPerLine);
                    if (moduleProcList.Count % positionsPerLine != 0 && moduleProcList.Count > positionsPerLine)
                    {
                    }
                }
            }
            if (linesCount == 0 && totalProcesses > 0)
            {
                linesCount = (int)Math.Ceiling((double)totalProcesses / positionsPerLine);
            }
            linesCount = Math.Max(1, linesCount);


            int width = Math.Max(800, (int)(positionsPerLine * processHorizontalSize + (positionsPerLine - 1) * processHorizontalDistance + processHorizontalDistance + titleLength));
            int height = Math.Max(600, (int)(linesCount * dynamicLineHeight + (linesCount) * lineDistance + lineDistance * 2));


            Bitmap bitmap = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.FillRectangle(Brushes.White, 0, 0, width, height);

                int currentLine = 0;
                int currentPositionInLine = 0;

                for (int moduleIndex = 0; moduleIndex < network.ModuleProcesses.Length; moduleIndex++)
                {
                    var moduleProcesses = network.ModuleProcesses[moduleIndex];
                    if (moduleProcesses.Count == 0) continue;

                    string moduleTitle = moduleIndex < network.SourceDiagram.SystemData.NumProduction
                        ? $"ГВМ{moduleIndex + 1}"
                        : $"АТМ{moduleIndex - network.SourceDiagram.SystemData.NumProduction + 1}";

                    float titleX = GetPosX(0, processHorizontalSize, processHorizontalDistance, titleLength) - titleLength + titleLength * 0.1f;
                    float titleY = GetPosY(currentLine, dynamicLineHeight, lineDistance) + dynamicLineHeight * 0.4f - fontTitle.Height / 2;
                    g.DrawString(moduleTitle, fontTitle, Brushes.Black, titleX, titleY);


                    foreach (var process in moduleProcesses)
                    {
                        if (currentPositionInLine >= positionsPerLine)
                        {
                            currentPositionInLine = 0;
                            currentLine++;
                        }

                        float processBaseX = GetPosX(currentPositionInLine, processHorizontalSize, processHorizontalDistance, titleLength);
                        float processBaseY = GetPosY(currentLine, dynamicLineHeight, lineDistance);

                        int layoutIndex = 0;
                        foreach (var inputPos in process.InputPositions)
                        {
                            DrawPosition(g, inputPos, processBaseX, processBaseY, layoutIndex, circleSize, circleDistance,
                                         horizontalCircleDistance, arrowHorizontalLength, arrowVerticalLength,
                                         blackPen, fontCircle, true, false, positionCoords);
                            layoutIndex++;
                        }
                        foreach (var inhibitorPos in process.InhibitorPositions)
                        {
                            DrawPosition(g, inhibitorPos, processBaseX, processBaseY, layoutIndex, circleSize, circleDistance,
                                         horizontalCircleDistance, arrowHorizontalLength, arrowVerticalLength,
                                         blackPen, fontCircle, true, true, positionCoords);
                            layoutIndex++;
                        }

                        int maxInputs = process.InputPositions.Count + process.InhibitorPositions.Count;
                        int maxOutputs = process.OutputPositions.Count;
                        int numSlotsForThisTransition = Math.Max(1, Math.Max(maxInputs, maxOutputs));
                        float processBarHeight = numSlotsForThisTransition * circleSize + Math.Max(0, numSlotsForThisTransition - 1) * circleDistance;

                        float transitionBarX = processBaseX + circleSize + horizontalCircleDistance;
                        g.FillRectangle(Brushes.Black, transitionBarX - 1f, processBaseY, 3f, processBarHeight);
                        g.DrawString(process.Name, fontLine, Brushes.Black,
                                     transitionBarX + 1.5f - g.MeasureString(process.Name, fontLine).Width / 2,
                                     processBaseY - fontLine.Height - 2);

                        layoutIndex = 0;
                        foreach (var outputPos in process.OutputPositions)
                        {
                            DrawPosition(g, outputPos, processBaseX, processBaseY, layoutIndex, circleSize, circleDistance,
                                         horizontalCircleDistance, arrowHorizontalLength, arrowVerticalLength,
                                         blackPen, fontCircle, false, false, positionCoords);
                            layoutIndex++;
                        }
                        currentPositionInLine++;
                    }
                    if (currentPositionInLine != 0)
                    {
                        currentPositionInLine = 0;
                        currentLine++;
                    }
                }
            }

            fontCircle.Dispose();
            fontLine.Dispose();
            fontTitle.Dispose();
            blackPen.Dispose();
            return bitmap;
        }

        private float GetPosX(int currentPositionInLine, float itemWidth, float itemSpacing, float leftMargin)
        {
            return leftMargin + currentPositionInLine * (itemWidth + itemSpacing);
        }

        private float GetPosY(int currentLine, float itemHeight, float lineSpacing)
        {
            return lineSpacing + currentLine * (itemHeight + lineSpacing);
        }

        private void DrawPosition(Graphics g, PetriPosition position, float processBaseX, float processBaseY,
                        int layoutIndex, float circleSize, float circleDistance,
                        float horizontalCircleDistance, float arrowHorizontalLength,
                        float arrowVerticalLength, Pen pen, Font font,
                        bool isInput, bool isInhibitor,
                        Dictionary<PetriPosition, PointF> positionCoords)
        {
            float slotTopY = processBaseY + CalcShift(layoutIndex, circleSize, circleDistance);
            float slotCenterY = slotTopY + circleSize * 0.5f;

            PointF currentCircleCenter;
            PointF transitionAttachmentPoint;
            float transitionBarWidth = 3f;

            if (isInput)
            {
                float inputCircleX_TopLeft = processBaseX;
                currentCircleCenter = new PointF(inputCircleX_TopLeft + circleSize * 0.5f, slotCenterY);
                transitionAttachmentPoint = new PointF(processBaseX + circleSize + horizontalCircleDistance, slotCenterY);
            }
            else
            {
                float outputCircleX_TopLeft = processBaseX + circleSize + horizontalCircleDistance + transitionBarWidth + horizontalCircleDistance;
                currentCircleCenter = new PointF(outputCircleX_TopLeft + circleSize * 0.5f, slotCenterY);
                transitionAttachmentPoint = new PointF(processBaseX + circleSize + horizontalCircleDistance + transitionBarWidth, slotCenterY);
            }

            bool useSharedCoordinatesLogic = (position.Class == PositionClass.Production || position.Class == PositionClass.Transport);

            if (useSharedCoordinatesLogic && positionCoords.TryGetValue(position, out PointF existingCenter))
            {
                if (isInput)
                {
                    DrawArrowLogic(g, pen, existingCenter, transitionAttachmentPoint, arrowHorizontalLength, arrowVerticalLength, isInhibitor, circleSize * 0.5f, false);
                }
                else
                {
                    DrawArrowLogic(g, pen, transitionAttachmentPoint, existingCenter, arrowHorizontalLength, arrowVerticalLength, false, circleSize * 0.5f, true);
                }
            }
            else
            {
                float circleDrawX = currentCircleCenter.X - circleSize * 0.5f;
                float circleDrawY = currentCircleCenter.Y - circleSize * 0.5f;
                g.DrawEllipse(pen, circleDrawX, circleDrawY, circleSize, circleSize);

                SizeF textSize = g.MeasureString(position.Name, font);
                float textX = circleDrawX + (circleSize - textSize.Width) / 2;
                float textY = circleDrawY + (circleSize - textSize.Height) / 2;
                g.DrawString(position.Name, font, Brushes.Black, textX, textY);

                if (useSharedCoordinatesLogic)
                {
                    positionCoords[position] = currentCircleCenter;
                }

                if (isInput)
                {
                    DrawArrowLogic(g, pen, currentCircleCenter, transitionAttachmentPoint, arrowHorizontalLength, arrowVerticalLength, isInhibitor, circleSize * 0.5f, false);
                }
                else
                {
                    DrawArrowLogic(g, pen, transitionAttachmentPoint, currentCircleCenter, arrowHorizontalLength, arrowVerticalLength, false, circleSize * 0.5f, true);
                }
            }
        }


        private void DrawArrowLogic(Graphics g, Pen pen, PointF fromPoint, PointF toPoint,
                                    float arrowSideLength, float inhibitorCircleRadiusParam,
                                    bool isForInhibitor, float petriNetCircleRadius, bool isLineToPetriCircle)
        {
            PointF actualLineFrom = fromPoint;
            PointF actualLineTo = toPoint;
            double angle = Math.Atan2(toPoint.Y - fromPoint.Y, toPoint.X - fromPoint.X);

            if (!isLineToPetriCircle)
            {
                actualLineFrom = new PointF(
                    (float)(fromPoint.X + petriNetCircleRadius * Math.Cos(angle)),
                    (float)(fromPoint.Y + petriNetCircleRadius * Math.Sin(angle))
                );
            }
            else
            {
                actualLineTo = new PointF(
                    (float)(toPoint.X - petriNetCircleRadius * Math.Cos(angle)),
                    (float)(toPoint.Y - petriNetCircleRadius * Math.Sin(angle))
                );
            }

            if (Math.Sqrt(Math.Pow(actualLineTo.X - actualLineFrom.X, 2) + Math.Pow(actualLineTo.Y - actualLineFrom.Y, 2)) < 1.0)
            {
                return;
            }

            g.DrawLine(pen, actualLineFrom, actualLineTo);

            if (isForInhibitor)
            {
                float inhibitorRadius = inhibitorCircleRadiusParam;
                PointF inhibitorCircleCenter = actualLineTo;

                g.FillEllipse(Brushes.White, inhibitorCircleCenter.X - inhibitorRadius, inhibitorCircleCenter.Y - inhibitorRadius, inhibitorRadius * 2, inhibitorRadius * 2);
                g.DrawEllipse(pen, inhibitorCircleCenter.X - inhibitorRadius, inhibitorCircleCenter.Y - inhibitorRadius, inhibitorRadius * 2, inhibitorRadius * 2);
            }
            else
            {
                PointF[] arrowHead = new PointF[3];
                arrowHead[0] = actualLineTo;

                double arrowTipAngle = Math.Atan2(actualLineFrom.Y - actualLineTo.Y, actualLineFrom.X - actualLineTo.X); 
                arrowHead[1] = new PointF(
                    (float)(actualLineTo.X + arrowSideLength * Math.Cos(arrowTipAngle + Math.PI / 7)),
                    (float)(actualLineTo.Y + arrowSideLength * Math.Sin(arrowTipAngle + Math.PI / 7))
                );
                arrowHead[2] = new PointF(
                    (float)(actualLineTo.X + arrowSideLength * Math.Cos(arrowTipAngle - Math.PI / 7)),
                    (float)(actualLineTo.Y + arrowSideLength * Math.Sin(arrowTipAngle - Math.PI / 7))
                );
                g.FillPolygon(Brushes.Black, arrowHead);
            }
        }
    }
}