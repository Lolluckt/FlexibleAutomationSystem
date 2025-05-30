namespace FlexibleAutomationSystem.Models
{
    public enum ProductionPriorityRule
    {
        ShortestOperation,
        LongestOperation,
        MinRemainingWork,
        MaxRemainingWork,
        BalancedLoad
    }

    public enum TransportPriorityRule
    {
        MaximizeLoad,
        MinimizeLoad,
        NearestTransport
    }

    public enum DiagramType
    {
        Standard,
        Extended
    }

    public class Process
    {
        public int ModuleIndex { get; set; }
        public int OperationIndex { get; set; }
        public double ProcessTime { get; set; }
        public string Description { get; set; }
        public int FromModule { get; set; }
        public int ToModule { get; set; }

        public Process(int module, int operation, double time, string desc)
        {
            ModuleIndex = module;
            OperationIndex = operation;
            ProcessTime = time;
            Description = desc;
        }

        public Process(int module, int operation, double time, string desc, int from, int to)
            : this(module, operation, time, desc)
        {
            FromModule = from;
            ToModule = to;
        }
    }

    public class Task
    {
        public int DetailIndex { get; set; }
        public int OperationIndex { get; set; }
        public double ProcessTime { get; set; }
        public string Description { get; set; }
        public int FromModule { get; set; }
        public int ToModule { get; set; }

        public Task(int detail, int operation, double time, string desc)
        {
            DetailIndex = detail;
            OperationIndex = operation;
            ProcessTime = time;
            Description = desc;
        }

        public Task(int detail, int operation, double time, string desc, int from, int to)
            : this(detail, operation, time, desc)
        {
            FromModule = from;
            ToModule = to;
        }
    }

    public class Work
    {
        public int DetailIndex { get; set; }
        public int OperationIndex { get; set; }
        public double ProcessTime { get; set; }
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public string Description { get; set; }
        public int FromModule { get; set; }
        public int ToModule { get; set; }

        public Work(int detail, int operation, double processTime, string desc, double start, double end)
        {
            DetailIndex = detail;
            OperationIndex = operation;
            ProcessTime = processTime;
            Description = desc;
            StartTime = start;
            EndTime = end;
        }

        public Work ClonePlaceholder() =>
            new Work(DetailIndex, OperationIndex, 0, string.Empty,
                double.MaxValue, double.MaxValue);

        public Work(int detail, int operation, double processTime, string desc, double start, double end, int from, int to)
            : this(detail, operation, processTime, desc, start, end)
        {
            FromModule = from;
            ToModule = to;
        }
    }

    public class GanttDiagram
    {
        public List<Process>[] DetailProcesses { get; set; }
        public List<Process>[] DetailProcessesExtended { get; set; }
        public List<Task>[] ModuleTasks { get; set; }
        public List<Work>[] ModuleWorks { get; set; }

        public int[] DetailProcessCurrent { get; set; }
        public int[] DetailProcessCurrentExtended { get; set; }
        public int[] ModuleWorksCurrent { get; set; }

        public bool[] ModuleFree { get; set; }
        public bool[] DetailFree { get; set; }
        public bool[] ModuleCollectorEntryFree { get; set; }
        public bool[] ModuleCollectorExitFree { get; set; }

        public int[] TransportLastModule { get; set; }
        public bool StorageFree { get; set; }
        public double StorageFreeTime { get; set; }

        public double CurrentTime { get; set; }
        public double CycleTime { get; set; }

        public ProductionPriorityRule ProductionRule { get; set; }
        public TransportPriorityRule TransportRule { get; set; }
        public DiagramType Type { get; set; }
        public SystemData SystemData { get; set; }

        public bool IsCalculated { get; set; }
        public bool DrawGrayMode { get; set; }
        public bool DrawDetails { get; set; }

        public GanttDiagram(SystemData data)
        {
            SystemData = data;
            InitializeArrays();
        }

        private void InitializeArrays()
        {
            int detailCount = SystemData.NumDetails;
            int moduleCount = SystemData.NumProduction + SystemData.NumTransport;

            DetailProcesses = new List<Process>[detailCount];
            DetailProcessesExtended = new List<Process>[detailCount];
            ModuleTasks = new List<Task>[moduleCount];
            ModuleWorks = new List<Work>[moduleCount];

            for (int i = 0; i < detailCount; i++)
            {
                DetailProcesses[i] = new List<Process>();
                DetailProcessesExtended[i] = new List<Process>();
            }

            for (int i = 0; i < moduleCount; i++)
            {
                ModuleTasks[i] = new List<Task>();
                ModuleWorks[i] = new List<Work>();
            }

            DetailProcessCurrent = new int[detailCount];
            DetailProcessCurrentExtended = new int[detailCount];
            ModuleWorksCurrent = new int[moduleCount];
            ModuleFree = new bool[moduleCount];
            DetailFree = new bool[detailCount];
            ModuleCollectorEntryFree = new bool[moduleCount];
            ModuleCollectorExitFree = new bool[moduleCount];
            TransportLastModule = new int[SystemData.NumTransport];

            for (int i = 0; i < moduleCount; i++)
            {
                ModuleFree[i] = true;
                ModuleCollectorEntryFree[i] = true;
                ModuleCollectorExitFree[i] = true;
            }

            for (int i = 0; i < detailCount; i++)
            {
                DetailFree[i] = true;
            }

            StorageFree = true;
            StorageFreeTime = 0;
            CurrentTime = 0;
            CycleTime = 0;
            IsCalculated = false;
            DrawGrayMode = false;
            DrawDetails = false;
        }
    }
}