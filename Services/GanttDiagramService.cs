using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FlexibleAutomationSystem.Models;
using Process = FlexibleAutomationSystem.Models.Process;
using Task = FlexibleAutomationSystem.Models.Task;

namespace FlexibleAutomationSystem.Services
{
    public class GanttDiagramService
    {
        private GanttDiagram _diagram;
        private List<Work>[] _standardPlan;

        public GanttDiagram Calculate(SystemData data, ProductionPriorityRule prodRule,
                                     TransportPriorityRule transRule, DiagramType diagramType)
        {
            _diagram = new GanttDiagram(data)
            {
                ProductionRule = prodRule,
                TransportRule = transRule,
                Type = diagramType
            };

            InitializeDetailProcesses();

            do
            {
                UpdateModuleTasks();
                DecideModuleWorks();
            }
            while (MoveToNextTimeEvent());

            _standardPlan = _diagram.ModuleWorks
                .Select(list => list.Select(w => w.ClonePlaceholder()).ToList())
                .ToArray();

            if (diagramType == DiagramType.Extended)
            {
                InitializeExtended();

                do
                {
                    UpdateModuleTasksExtended();
                    while (!DecideModuleWorksExtended())
                    {
                        UpdateModuleTasksExtended();
                    }
                }
                while (MoveToNextTimeEventExtended());
            }

            _diagram.IsCalculated = true;
            _diagram.CycleTime = _diagram.CurrentTime;

            return _diagram;
        }

        private void InitializeDetailProcesses()
        {
            var data = _diagram.SystemData;

            for (int i = 0; i < data.NumDetails; i++)
            {
                _diagram.DetailProcessCurrent[i] = 0;
                _diagram.DetailProcesses[i].Clear();

                int step = 0;
                while (step < data.NumSteps && data.DetailSteps[i, step] > 0)
                {
                    _diagram.DetailProcesses[i].Add(new Models.Process(
                        data.DetailSteps[i, step],
                        step,
                        data.DetailStepTimes[i, step],
                        $"ГВМ{data.DetailSteps[i, step]} обробляє Д{i + 1} на {step + 1} операції"
                    ));
                    step++;
                }
            }
        }

        private void UpdateModuleTasks()
        {
            foreach (var tasks in _diagram.ModuleTasks)
            {
                tasks.Clear();
            }
            for (int detail = 0; detail < _diagram.SystemData.NumDetails; detail++)
            {
                if (_diagram.DetailFree[detail] &&
                    _diagram.DetailProcessCurrent[detail] < _diagram.DetailProcesses[detail].Count)
                {
                    var process = _diagram.DetailProcesses[detail][_diagram.DetailProcessCurrent[detail]];
                    if (process.ModuleIndex > 0)
                    {
                        _diagram.ModuleTasks[process.ModuleIndex - 1].Add(
                            new Models.Task(detail, process.OperationIndex, process.ProcessTime, process.Description)
                        );
                    }
                }
            }
        }

        private void DecideModuleWorks()
        {
            int moduleCount = _diagram.Type == DiagramType.Standard ?
                             _diagram.SystemData.NumProduction :
                             _diagram.SystemData.NumProduction + _diagram.SystemData.NumTransport;

            for (int module = 0; module < moduleCount; module++)
            {
                if (_diagram.ModuleFree[module] && _diagram.ModuleTasks[module].Count > 0)
                {
                    int taskIndex = DecidePriorityTask(module);
                    var task = _diagram.ModuleTasks[module][taskIndex];

                    _diagram.ModuleWorks[module].Add(new Work(
                        task.DetailIndex,
                        task.OperationIndex,
                        task.ProcessTime,
                        task.Description,
                        _diagram.CurrentTime,
                        _diagram.CurrentTime + task.ProcessTime
                    ));

                    _diagram.DetailProcessCurrent[task.DetailIndex]++;
                    _diagram.DetailFree[task.DetailIndex] = false;
                    _diagram.ModuleFree[module] = false;
                }
            }
        }

        private int DecidePriorityTask(int moduleIndex)
        {
            var tasks = _diagram.ModuleTasks[moduleIndex];

            if (tasks.Count == 1) return 0;
            if (moduleIndex < _diagram.SystemData.NumProduction)
            {
                return DecideProductionPriorityTask(moduleIndex);
            }
            return DecideTransportPriorityTask(moduleIndex);
        }

        private int DecideProductionPriorityTask(int moduleIndex)
        {
            var tasks = _diagram.ModuleTasks[moduleIndex];

            switch (_diagram.ProductionRule)
            {
                case ProductionPriorityRule.ShortestOperation:
                    return tasks.IndexOf(tasks.OrderBy(t => t.ProcessTime).First());

                case ProductionPriorityRule.LongestOperation:
                    return tasks.IndexOf(tasks.OrderByDescending(t => t.ProcessTime).First());

                case ProductionPriorityRule.MinRemainingWork:
                    {
                        double minRemaining = double.MaxValue;
                        int bestIndex = 0;

                        for (int i = 0; i < tasks.Count; i++)
                        {
                            double remaining = CalculateRemainingWork(tasks[i].DetailIndex);
                            if (remaining < minRemaining)
                            {
                                minRemaining = remaining;
                                bestIndex = i;
                            }
                        }
                        return bestIndex;
                    }

                case ProductionPriorityRule.MaxRemainingWork:
                    {
                        double maxRemaining = 0;
                        int bestIndex = 0;

                        for (int i = 0; i < tasks.Count; i++)
                        {
                            double remaining = CalculateRemainingWork(tasks[i].DetailIndex);
                            if (remaining > maxRemaining)
                            {
                                maxRemaining = remaining;
                                bestIndex = i;
                            }
                        }
                        return bestIndex;
                    }

                case ProductionPriorityRule.BalancedLoad:
                    {
                        double minLoad = double.MaxValue;
                        int bestIndex = 0;

                        for (int i = 0; i < tasks.Count; i++)
                        {
                            int nextModule = GetNextModule(tasks[i].DetailIndex);
                            if (nextModule > 0)
                            {
                                double load = CalculateModuleLoad(nextModule - 1);
                                if (load < minLoad)
                                {
                                    minLoad = load;
                                    bestIndex = i;
                                }
                            }
                        }
                        return bestIndex;
                    }

                default:
                    return 0;
            }
        }

        private int DecideTransportPriorityTask(int moduleIndex)
        {
            return 0;
        }

        private double CalculateRemainingWork(int detailIndex)
        {
            double remaining = 0;
            var processes = _diagram.DetailProcesses[detailIndex];

            for (int i = _diagram.DetailProcessCurrent[detailIndex]; i < processes.Count; i++)
            {
                remaining += processes[i].ProcessTime;
            }

            return remaining;
        }

        private int GetNextModule(int detailIndex)
        {
            var processes = _diagram.DetailProcesses[detailIndex];
            int current = _diagram.DetailProcessCurrent[detailIndex];

            for (int i = current; i < processes.Count; i++)
            {
                if (processes[i].ModuleIndex > 0)
                {
                    return processes[i].ModuleIndex;
                }
            }

            return 0;
        }

        private double CalculateModuleLoad(int moduleIndex)
        {
            double load = 0;
            foreach (var work in _diagram.ModuleWorks[moduleIndex])
            {
                load += work.ProcessTime;
            }
            return load;
        }

        private bool MoveToNextTimeEvent()
        {
            double nextTime = double.MaxValue;
            for (int module = 0; module < _diagram.ModuleWorks.Length; module++)
            {
                if (!_diagram.ModuleFree[module] && _diagram.ModuleWorks[module].Count > 0)
                {
                    var lastWork = _diagram.ModuleWorks[module].Last();
                    if (lastWork.EndTime < nextTime)
                    {
                        nextTime = lastWork.EndTime;
                    }
                }
            }

            if (nextTime == double.MaxValue)
            {
                return false;
            }

            _diagram.CurrentTime = nextTime;
            for (int module = 0; module < _diagram.ModuleWorks.Length; module++)
            {
                if (!_diagram.ModuleFree[module] && _diagram.ModuleWorks[module].Count > 0)
                {
                    var lastWork = _diagram.ModuleWorks[module].Last();
                    if (lastWork.EndTime == nextTime)
                    {
                        _diagram.DetailFree[lastWork.DetailIndex] = true;
                        _diagram.ModuleFree[module] = true;
                        if (_diagram.DetailProcessCurrent[lastWork.DetailIndex] ==
                            _diagram.DetailProcesses[lastWork.DetailIndex].Count)
                        {
                            _diagram.DetailFree[lastWork.DetailIndex] = false;
                        }
                    }
                }
            }

            return true;
        }

        private void InitializeExtended()
        {
            var d = _diagram.SystemData;
            for (int det = 0; det < d.NumDetails; det++)
            {
                _diagram.DetailProcessCurrentExtended[det] = 0;
                var list = _diagram.DetailProcessesExtended[det];
                list.Clear();
                for (int op = 0; op < _diagram.DetailProcesses[det].Count; op++)
                {
                    int moduleIndex = _diagram.DetailProcesses[det][op].ModuleIndex;
                    double processTime = _diagram.DetailProcesses[det][op].ProcessTime;
                    int fromModule = (op == 0) ? 0 : _diagram.DetailProcesses[det][op - 1].ModuleIndex;
                    int toModule = moduleIndex;

                    list.Add(new Process(0, op,
                             d.TransportTimes[fromModule, toModule],
                             $"АТМ перевозить Д{det + 1} з {(fromModule == 0 ? "АС" : $"ГВМ{fromModule}")} на ГВМ{toModule}",
                             fromModule, toModule));

                    list.Add(new Process(moduleIndex, op, processTime,
                             $"ГВМ{moduleIndex} обробляє Д{det + 1} на {op + 1} операції"));
                }

                if (_diagram.DetailProcesses[det].Count > 0)
                {
                    int lastOp = _diagram.DetailProcesses[det].Count - 1;
                    int lastModule = _diagram.DetailProcesses[det][lastOp].ModuleIndex;

                    list.Add(new Process(0, lastOp + 1,
                             d.TransportTimes[lastModule, 0],
                             $"АТМ перевозить Д{det + 1} з ГВМ{lastModule} на АС",
                             lastModule, 0));
                }
            }

            Array.Fill(_diagram.DetailFree, true);
            Array.Fill(_diagram.ModuleFree, true);
            Array.Fill(_diagram.DetailProcessCurrent, 0);
            Array.Fill(_diagram.DetailProcessCurrentExtended, 0);
            _diagram.CurrentTime = 0;
            _diagram.StorageFree = true;
            _diagram.StorageFreeTime = 0;
            Array.Clear(_diagram.ModuleWorksCurrent, 0, _diagram.ModuleWorksCurrent.Length);
            Array.Clear(_diagram.TransportLastModule, 0, _diagram.TransportLastModule.Length);

            foreach (var list in _diagram.ModuleTasks) list.Clear();
            foreach (var list in _diagram.ModuleWorks) list.Clear();

            for (int i = 0; i < d.NumProduction; i++)
            {
                _diagram.ModuleCollectorEntryFree[i] = true;
                _diagram.ModuleCollectorExitFree[i] = true;
            }
        }

        private void UpdateModuleTasksExtended()
        {
            foreach (var t in _diagram.ModuleTasks) t.Clear();
            var d = _diagram.SystemData;
            for (int det = 0; det < d.NumDetails; det++)
            {
                if (!_diagram.DetailFree[det]) continue;
                if (_diagram.DetailProcessCurrentExtended[det] >= _diagram.DetailProcessesExtended[det].Count) continue;

                var currentProc = _diagram.DetailProcessesExtended[det][_diagram.DetailProcessCurrentExtended[det]];

                if (currentProc.ModuleIndex > 0)
                {
                    int prod = currentProc.ModuleIndex - 1;
                    if (_diagram.ModuleFree[prod])
                    {
                        _diagram.ModuleTasks[prod].Add(new Task(
                            det, currentProc.OperationIndex, currentProc.ProcessTime, currentProc.Description));
                    }
                }
                else
                {
                    for (int tr = 0; tr < d.NumTransport; tr++)
                    {
                        int tm = d.NumProduction + tr;
                        if (_diagram.ModuleFree[tm])
                        {
                            _diagram.ModuleTasks[tm].Add(new Task(
                                det, currentProc.OperationIndex, currentProc.ProcessTime,
                                currentProc.Description.Replace("АТМ", $"АТМ{tr + 1}"),
                                currentProc.FromModule, currentProc.ToModule));
                        }
                    }
                }
            }
        }

        private bool DecideModuleWorksExtended()
        {
            DecideModuleWorksExtendedProduction();
            DecideModuleWorksExtendedTransport();
            return true;
        }

        private bool DecideModuleWorksExtendedProduction()
        {
            var d = _diagram.SystemData;

            for (int prod = 0; prod < d.NumProduction; prod++)
            {
                if (!_diagram.ModuleFree[prod]) continue;
                if (_diagram.ModuleTasks[prod].Count == 0) continue;
                int idx = DecidePriorityTask(prod);
                var task = _diagram.ModuleTasks[prod][idx];

                _diagram.ModuleWorks[prod].Add(new Work(
                    task.DetailIndex, task.OperationIndex, task.ProcessTime,
                    task.Description, _diagram.CurrentTime,
                    _diagram.CurrentTime + task.ProcessTime));

                _diagram.ModuleFree[prod] = false;
                _diagram.DetailFree[task.DetailIndex] = false;
                _diagram.DetailProcessCurrent[task.DetailIndex]++;
                _diagram.DetailProcessCurrentExtended[task.DetailIndex]++;
            }

            return true;
        }

        private void DecideModuleWorksExtendedTransport()
        {
            var d = _diagram.SystemData;
            for (int tr = 0; tr < d.NumTransport; tr++)
            {
                int tm = d.NumProduction + tr;

                if (!_diagram.ModuleFree[tm]) continue;
                if (_diagram.ModuleTasks[tm].Count == 0) continue;
                var task = _diagram.ModuleTasks[tm][0];
                double totalTransportTime = task.ProcessTime;
                string combinedDescription = task.Description;

                if (_diagram.DetailProcessCurrentExtended[task.DetailIndex] + 1 < _diagram.DetailProcessesExtended[task.DetailIndex].Count)
                {
                    var nextProc = _diagram.DetailProcessesExtended[task.DetailIndex][_diagram.DetailProcessCurrentExtended[task.DetailIndex] + 1];
                    if (nextProc.ModuleIndex > 0)
                    {
                        totalTransportTime = Math.Max(totalTransportTime, 5.0);
                    }
                }
                _diagram.ModuleWorks[tm].Add(new Work(
                    task.DetailIndex, task.OperationIndex, totalTransportTime, combinedDescription,
                    _diagram.CurrentTime, _diagram.CurrentTime + totalTransportTime,
                    task.FromModule, task.ToModule));

                _diagram.ModuleFree[tm] = false;
                _diagram.DetailFree[task.DetailIndex] = false;
                _diagram.DetailProcessCurrentExtended[task.DetailIndex]++;
                for (int i = d.NumProduction; i < d.NumProduction + d.NumTransport; i++)
                {
                    _diagram.ModuleTasks[i].RemoveAll(t =>
                        t.DetailIndex == task.DetailIndex &&
                        t.OperationIndex == task.OperationIndex &&
                        t.FromModule == task.FromModule &&
                        t.ToModule == task.ToModule);
                }

                return;
            }
        }

        private bool MoveToNextTimeEventExtended()
        {
            double next = double.MaxValue;

            for (int m = 0; m < _diagram.ModuleWorks.Length; m++)
            {
                if (_diagram.ModuleFree[m] || _diagram.ModuleWorks[m].Count == 0) continue;
                double end = _diagram.ModuleWorks[m].Last().EndTime;
                if (end < next) next = end;
            }

            if (next == double.MaxValue) return false;

            _diagram.CurrentTime = next;

            for (int m = 0; m < _diagram.ModuleWorks.Length; m++)
            {
                if (_diagram.ModuleFree[m] || _diagram.ModuleWorks[m].Count == 0) continue;

                var lastWork = _diagram.ModuleWorks[m].Last();
                if (Math.Abs(lastWork.EndTime - next) < 1e-9)
                {
                    _diagram.ModuleFree[m] = true;
                    _diagram.DetailFree[lastWork.DetailIndex] = true;
                    bool allDone = _diagram.DetailProcessCurrentExtended[lastWork.DetailIndex] >=
                                   _diagram.DetailProcessesExtended[lastWork.DetailIndex].Count;

                    if (allDone)
                    {
                        _diagram.DetailFree[lastWork.DetailIndex] = false;
                    }
                }
            }

            return true;
        }

        private int DecidePriorityTransportModule(int taskIndex)
        {
            var data = _diagram.SystemData;
            var baseTasks = _diagram.ModuleTasks[data.NumProduction];
            if (baseTasks.Count == 0) return data.NumProduction;
            if (taskIndex >= baseTasks.Count) taskIndex = baseTasks.Count - 1;

            var task = baseTasks[taskIndex];

            switch (_diagram.TransportRule)
            {
                case TransportPriorityRule.MinimizeLoad:
                    {
                        double minLoad = double.MaxValue;
                        int bestModule = data.NumProduction;

                        for (int i = data.NumProduction; i < data.NumProduction + data.NumTransport; i++)
                        {
                            if (_diagram.ModuleFree[i])
                            {
                                double load = CalculateModuleLoad(i);
                                if (load < minLoad)
                                {
                                    minLoad = load;
                                    bestModule = i;
                                }
                            }
                        }
                        return bestModule;
                    }

                case TransportPriorityRule.MaximizeLoad:
                    {
                        double maxLoad = -1;
                        int bestModule = data.NumProduction;

                        for (int i = data.NumProduction; i < data.NumProduction + data.NumTransport; i++)
                        {
                            if (_diagram.ModuleFree[i])
                            {
                                double load = CalculateModuleLoad(i);
                                if (load > maxLoad)
                                {
                                    maxLoad = load;
                                    bestModule = i;
                                }
                            }
                        }
                        return bestModule;
                    }

                case TransportPriorityRule.NearestTransport:
                    {
                        double minDistance = double.MaxValue;
                        int bestModule = data.NumProduction;

                        for (int trans = 0; trans < data.NumTransport; trans++)
                        {
                            int moduleIndex = data.NumProduction + trans;
                            if (_diagram.ModuleFree[moduleIndex])
                            {
                                int lastModule = _diagram.TransportLastModule[trans];
                                double distance = data.TransportDistances[lastModule, task.FromModule];

                                if (distance < minDistance)
                                {
                                    minDistance = distance;
                                    bestModule = moduleIndex;
                                }
                            }
                        }
                        return bestModule;
                    }

                default:
                    return data.NumProduction;
            }
        }

        private bool IsModuleCollectorEntryFree(int moduleIndex)
        {
            if (moduleIndex < 0 || moduleIndex >= _diagram.ModuleCollectorEntryFree.Length)
                return true;

            if (_diagram.SystemData.ProductionCellCount[moduleIndex] == 1)
                return _diagram.ModuleCollectorEntryFree[moduleIndex] &&
                       _diagram.ModuleCollectorExitFree[moduleIndex];

            return _diagram.ModuleCollectorEntryFree[moduleIndex];
        }

        private bool IsModuleCollectorExitFree(int moduleIndex)
        {
            if (moduleIndex < 0 || moduleIndex >= _diagram.ModuleCollectorExitFree.Length)
                return true;

            return _diagram.ModuleCollectorExitFree[moduleIndex];
        }
    }
}