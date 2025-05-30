using System.Collections.Generic;

namespace FlexibleAutomationSystem.Models
{
    public class PetriProcess
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int Module { get; set; }
        public int Detail { get; set; }
        public int Operation { get; set; }
        public int ModuleFrom { get; set; }
        public int ModuleTo { get; set; }

        public List<PetriPosition> InputPositions { get; set; }
        public List<PetriPosition> OutputPositions { get; set; }
        public List<PetriPosition> InhibitorPositions { get; set; }

        public PetriProcess(string name, string desc)
        {
            Name = name;
            Description = desc;
            InputPositions = new List<PetriPosition>();
            OutputPositions = new List<PetriPosition>();
            InhibitorPositions = new List<PetriPosition>();
        }

        public PetriProcess(string name, string desc, int module, int detail, int operation)
            : this(name, desc)
        {
            Module = module;
            Detail = detail;
            Operation = operation;
        }

        public PetriProcess(string name, string desc, int module, int detail, int operation, int moduleFrom, int moduleTo)
            : this(name, desc, module, detail, operation)
        {
            ModuleFrom = moduleFrom;
            ModuleTo = moduleTo;
        }
    }

    public enum PositionClass
    {
        Production,
        Transport,
        Detail
    }

    public enum CollectorType
    {
        Entry,
        Exit
    }

    public class PetriPosition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public PositionClass Class { get; set; }
        public CollectorType Collector { get; set; }
        public int Module { get; set; }
        public int Detail { get; set; }
        public int Operation { get; set; }
        public int ModuleFrom { get; set; }
        public int ModuleTo { get; set; }

        public PetriPosition(string name, string desc, PositionClass posClass)
        {
            Name = name;
            Description = desc;
            Class = posClass;
        }

        public PetriPosition(string name, string desc, PositionClass posClass, CollectorType collector)
            : this(name, desc, posClass)
        {
            Collector = collector;
        }

        public PetriPosition(string name, string desc, PositionClass posClass, CollectorType collector,
                           int module, int detail, int operation)
            : this(name, desc, posClass, collector)
        {
            Module = module;
            Detail = detail;
            Operation = operation;
        }

        public PetriPosition(string name, string desc, PositionClass posClass, int module,
                           int detail, int operation)
            : this(name, desc, posClass)
        {
            Module = module;
            Detail = detail;
            Operation = operation;
        }

        public PetriPosition(string name, string desc, PositionClass posClass, int module,
                           int detail, int operation, int moduleFrom, int moduleTo)
            : this(name, desc, posClass, module, detail, operation)
        {
            ModuleFrom = moduleFrom;
            ModuleTo = moduleTo;
        }
    }

    public class PetriNetwork
    {
        public List<PetriProcess> Processes { get; set; }
        public List<PetriPosition> Positions { get; set; }
        public List<PetriPosition> InitialPositions { get; set; }
        public List<PetriPosition> FinalPositions { get; set; }

        public List<PetriProcess>[] ModuleProcesses { get; set; }
        public List<PetriPosition>[] ProductionPositions { get; set; }
        public List<PetriPosition>[] TransportPositions { get; set; }
        public List<PetriPosition>[] DetailPositions { get; set; }

        public GanttDiagram SourceDiagram { get; set; }

        public PetriNetwork(GanttDiagram diagram)
        {
            SourceDiagram = diagram;
            Processes = new List<PetriProcess>();
            Positions = new List<PetriPosition>();
            InitialPositions = new List<PetriPosition>();
            FinalPositions = new List<PetriPosition>();

            InitializeArrays();
        }

        private void InitializeArrays()
        {
            int moduleCount = SourceDiagram.ModuleWorks.Length;
            int prodCount = SourceDiagram.SystemData.NumProduction;
            int transCount = SourceDiagram.SystemData.NumTransport;
            int detailCount = SourceDiagram.SystemData.NumDetails;

            ModuleProcesses = new List<PetriProcess>[moduleCount];
            for (int i = 0; i < moduleCount; i++)
            {
                ModuleProcesses[i] = new List<PetriProcess>();
            }

            ProductionPositions = new List<PetriPosition>[prodCount];
            for (int i = 0; i < prodCount; i++)
            {
                ProductionPositions[i] = new List<PetriPosition>();
            }

            TransportPositions = new List<PetriPosition>[transCount];
            for (int i = 0; i < transCount; i++)
            {
                TransportPositions[i] = new List<PetriPosition>();
            }

            DetailPositions = new List<PetriPosition>[detailCount];
            for (int i = 0; i < detailCount; i++)
            {
                DetailPositions[i] = new List<PetriPosition>();
            }
        }
    }
}