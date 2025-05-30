namespace FlexibleAutomationSystem.Models
{
    public class SystemData
    {
        public int NumDetails { get; set; }
        public int NumSteps { get; set; }
        public int NumProduction { get; set; }
        public int NumTransport { get; set; }

        public double TimeLoad { get; set; }
        public double TimeUnload { get; set; }
        public double TimeGiveTake { get; set; }
        public double AverageSpeed { get; set; }

        public int[,] DetailSteps { get; set; }
        public double[,] DetailStepTimes { get; set; }
        public double[,] TransportDistances { get; set; }
        public double[,] TransportTimes { get; set; }
        public int[] ProductionCellCount { get; set; }

        public SystemData()
        {
            NumDetails = 14;
            NumSteps = 5;
            NumProduction = 5;
            NumTransport = 3;

            TimeLoad = 0;
            TimeUnload = 0;
            TimeGiveTake = 0;
            AverageSpeed = 1;

            InitializeArrays();
        }

        private void InitializeArrays()
        {
            DetailSteps = new int[NumDetails, NumSteps];
            DetailStepTimes = new double[NumDetails, NumSteps];
            TransportDistances = new double[NumProduction + 1, NumProduction + 1];
            TransportTimes = new double[NumProduction + 1, NumProduction + 1];
            ProductionCellCount = new int[NumProduction];

            for (int i = 0; i < NumProduction; i++)
            {
                ProductionCellCount[i] = 2;
            }
        }

        public void CalculateTransportTimes()
        {
            for (int i = 0; i <= NumProduction; i++)
            {
                for (int j = 0; j <= NumProduction; j++)
                {
                    TransportTimes[i, j] = TransportDistances[i, j] / AverageSpeed + 2 * TimeGiveTake;

                    if (i != 0) TransportTimes[i, j] += TimeUnload;
                    if (j != 0) TransportTimes[i, j] += TimeLoad;
                }
            }
        }
    }
}