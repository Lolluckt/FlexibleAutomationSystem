using System;
using System.IO;
using FlexibleAutomationSystem.Models;

namespace FlexibleAutomationSystem.Services
{
    public class DataManager
    {
        public void SaveData(SystemData data, string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine(data.NumDetails);
                writer.WriteLine(data.NumProduction);
                writer.WriteLine(data.NumTransport);
                writer.WriteLine(data.NumSteps);
                writer.WriteLine(data.TimeLoad);
                writer.WriteLine(data.TimeUnload);
                writer.WriteLine(data.TimeGiveTake);
                writer.WriteLine(data.AverageSpeed);

                for (int i = 0; i < data.NumDetails; i++)
                {
                    for (int j = 0; j < data.NumSteps; j++)
                    {
                        writer.Write(data.DetailSteps[i, j] + " ");
                    }
                    writer.WriteLine();
                }

                for (int i = 0; i < data.NumDetails; i++)
                {
                    for (int j = 0; j < data.NumSteps; j++)
                    {
                        writer.Write(data.DetailStepTimes[i, j] + " ");
                    }
                    writer.WriteLine();
                }

                for (int i = 0; i < data.NumProduction + 1; i++)
                {
                    for (int j = 0; j < data.NumProduction + 1; j++)
                    {
                        writer.Write(data.TransportDistances[i, j] + " ");
                    }
                    writer.WriteLine();
                }

                for (int i = 0; i < data.NumProduction; i++)
                {
                    writer.Write(data.ProductionCellCount[i] + " ");
                }
                writer.WriteLine();

                for (int i = 0; i < data.NumProduction + 1; i++)
                {
                    for (int j = 0; j < data.NumProduction + 1; j++)
                    {
                        writer.Write(data.TransportTimes[i, j] + " ");
                    }
                    writer.WriteLine();
                }
            }
        }

        public SystemData LoadData(string filePath)
        {
            var data = new SystemData();

            using (var reader = new StreamReader(filePath))
            {
                data.NumDetails = Convert.ToInt32(reader.ReadLine());
                data.NumProduction = Convert.ToInt32(reader.ReadLine());
                data.NumTransport = Convert.ToInt32(reader.ReadLine());
                data.NumSteps = Convert.ToInt32(reader.ReadLine());
                data.TimeLoad = Convert.ToDouble(reader.ReadLine());
                data.TimeUnload = Convert.ToDouble(reader.ReadLine());
                data.TimeGiveTake = Convert.ToDouble(reader.ReadLine());
                data.AverageSpeed = Convert.ToDouble(reader.ReadLine());
                data.DetailSteps = new int[data.NumDetails, data.NumSteps];
                data.DetailStepTimes = new double[data.NumDetails, data.NumSteps];
                data.TransportDistances = new double[data.NumProduction + 1, data.NumProduction + 1];
                data.TransportTimes = new double[data.NumProduction + 1, data.NumProduction + 1];
                data.ProductionCellCount = new int[data.NumProduction];

                for (int i = 0; i < data.NumDetails; i++)
                {
                    string line = reader.ReadLine();
                    string[] values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (int j = 0; j < data.NumSteps; j++)
                    {
                        data.DetailSteps[i, j] = Convert.ToInt32(values[j]);
                    }
                }

                for (int i = 0; i < data.NumDetails; i++)
                {
                    string line = reader.ReadLine();
                    string[] values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (int j = 0; j < data.NumSteps; j++)
                    {
                        data.DetailStepTimes[i, j] = Convert.ToDouble(values[j]);
                    }
                }

                for (int i = 0; i < data.NumProduction + 1; i++)
                {
                    string line = reader.ReadLine();
                    string[] values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (int j = 0; j < data.NumProduction + 1; j++)
                    {
                        data.TransportDistances[i, j] = Convert.ToDouble(values[j]);
                    }
                }

                string prodLine = reader.ReadLine();
                string[] prodValues = prodLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < data.NumProduction; i++)
                {
                    data.ProductionCellCount[i] = Convert.ToInt32(prodValues[i]);
                }

                for (int i = 0; i < data.NumProduction + 1; i++)
                {
                    string line = reader.ReadLine();
                    string[] values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (int j = 0; j < data.NumProduction + 1; j++)
                    {
                        data.TransportTimes[i, j] = Convert.ToDouble(values[j]);
                    }
                }
            }

            return data;
        }
    }

    public class DataValidator
    {
        public bool ValidateSystemData(SystemData data)
        {
            if (data == null) return false;

            if (data.NumDetails < 1 || data.NumDetails > 21) return false;
            if (data.NumSteps < 1 || data.NumSteps > 14) return false;
            if (data.NumProduction < 1 || data.NumProduction > 10) return false;
            if (data.NumTransport < 1 || data.NumTransport > 10) return false;
            if (data.AverageSpeed <= 0) return false;
            if (data.TimeLoad < 0 || data.TimeUnload < 0 || data.TimeGiveTake < 0) return false;
            if (data.DetailSteps == null || data.DetailStepTimes == null) return false;
            if (data.TransportDistances == null || data.ProductionCellCount == null) return false;

            foreach (var count in data.ProductionCellCount)
            {
                if (count != 1 && count != 2) return false;
            }

            return true;
        }
    }
}