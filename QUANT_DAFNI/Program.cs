using System;
using System.Data;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json; //json

using QUANT2Core;
using QUANT2Core.models;
using QUANT2Core.statistics;
using QUANT2Core.utils;


namespace QUANT_DAFNI
{
    /// <summary>
    /// DAFNI entry point
    /// 
    /// For a calibrate:
    /// Inputs (datasets in environment variables):
    /// TObs1.bin
    /// TObs2.bin
    /// TObs3.bin
    /// dis1.bin
    /// dis2.bin
    /// dis3.bin
    /// Constraints coverage
    /// IsConstrained (bool)
    /// const string modelRunsDir = "../../../model-runs";
    /// const string TObsRoadFilename = "TObs_1.bin";
    /// const string TObsBusFilename = "TObs_2.bin";
    /// const string TObsGBRailFilename = "TObs_3.bin";
    /// const string DisRoadFilename = "dis_roads_min.bin";
    /// const string DisBusFilename = "dis_bus_min.bin";
    /// const string DisGBRailFilename = "dis_gbrail_min.bin";
    /// const string GreenBeltConstraintsFilename = "GreenBeltConstraints.bin";
    /// const string PopulationTable = "PopArea_KS101_MSOA.bin";
    /// const string ZoneCodesFilename = "EWS_ZoneCodes.bin";
    /// 
    /// Outputs:
    /// Statistics xml (contains betas)
    /// Constraints (if constrained set)
    /// TPred1
    /// TPred2
    /// TPred3
    /// //outputs
    /// const string StatisticsFilename = "StatisticsData2_QUANT3.xml";
    /// const string TPredRoadFilename = "TPred_Q3_1.bin";
    /// const string TPredBusFilename = "TPred_Q3_2.bin";
    /// const string TPredGBRailFilename = "TPred_Q3_3.bin";
    /// </summary>
    class Program
    {

        //outputs?
        //outputs
        const string StatisticsFilename = "StatisticsData2_QUANT3.xml";
        const string TPredRoadFilename = "TPred_Q3_1.bin";
        const string TPredBusFilename = "TPred_Q3_2.bin";
        const string TPredGBRailFilename = "TPred_Q3_3.bin";

        #region utils

        /// <summary>
        /// Write out a data table to a csv file
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="Filename"></param>
        private static void WriteCSV(DataTable dt, string Filename)
        {
            using (TextWriter writer = File.CreateText(Filename))
            {
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    if (i > 0) writer.Write(",");
                    writer.Write(dt.Columns[i].ColumnName);
                }
                writer.WriteLine("");

                foreach (DataRow row in dt.Rows)
                {
                    for (int i = 0; i < row.ItemArray.Length; i++)
                    {
                        string item = row.ItemArray[i].ToString();
                        if (i > 0) writer.Write(",");
                        writer.Write(item);
                    }
                    writer.WriteLine();
                }
            }
        }
        #endregion utils

        static void Main(string[] args)
        {
            //file copy
            //docker cp src dest
            //docker cp /tmp/config.ini grafana:/usr/share/grafana/conf/
            //
            //volume mount
            //docker run -d --name=grafana -p 3000:3000 grafana/grafana -v /tmp:/transfer
            //
            //bind mount
            //docker run -v /Users/andy/mydata:/mnt/mydata myimage
            //NOTE: only access to c:\users dir on windows

            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory) //or Directory.GetCurrentDirectory
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            IConfigurationRoot configuration = builder.Build();

            string ModelRunsDir = configuration["dirs:ModelRunsDir"];
            string OutputDir = configuration["dirs:OutputDir"];
            string TObsRoadFilename = configuration["matrices:TObs1"];
            string TObsBusFilename = configuration["matrices:TObs2"];
            string TObsGBRailFilename = configuration["matrices:TObs3"];
            string DisRoadFilename = configuration["matrices:dis_roads"];
            string DisBusFilename = configuration["matrices:dis_buses"];
            string DisGBRailFilename = configuration["matrices:dis_rail"];
            string GreenBeltConstraintsFilename = configuration["tables:GreenBeltConstraints"];
            string ConstraintsBFilename = configuration["tables:Constraints_B"];
            string PopulationTableFilename = configuration["tables:PopulationArea"];
            string ZoneCodesFilename = configuration["tables:ZoneCodes"];

            Console.WriteLine("Scanning inputs directory: " + ModelRunsDir);
            using (StreamWriter writer = File.CreateText(Path.Combine(OutputDir, "files.txt")))
            {
                EnumerationOptions enumops = new EnumerationOptions();
                enumops.RecurseSubdirectories = true;
                enumops.ReturnSpecialDirectories = true;
                string[] files = Directory.GetFiles("inputs","*.*",enumops); //hardcoded to inputs dir so we get everything that's in there regardless
                foreach (string name in files)
                {
                    Console.WriteLine("File: " + name);
                    writer.WriteLine("File: " + name);
                }
            }

            Console.WriteLine("QUANT_DAFNI: reading inputs...");
            //string mypath = Environment.GetEnvironmentVariable("PATH");
            //Console.WriteLine("path = " + mypath);

            //operation code
            string opcode = Environment.GetEnvironmentVariable("Q2_OpCode"); //calibrate, run etc. TODO: implement
            Console.WriteLine("QUANT_DAFNI: Q2_OpCode = " + opcode);

            //inputs
            Console.WriteLine("QUANT_DAFNI: ModelRunsDir = " + ModelRunsDir);
            Console.WriteLine("QUANT_DAFNI: OutputDir = " + OutputDir);
            Console.WriteLine("QUANT_DAFNI: TObsRoadFilename = " + TObsRoadFilename);
            Console.WriteLine("QUANT_DAFNI: TObsBusFilename = " + TObsBusFilename);
            Console.WriteLine("QUANT_DAFNI: TObsGBRailFilename = " + TObsGBRailFilename);
            Console.WriteLine("QUANT_DAFNI: DisRoadFilename = " + DisRoadFilename);
            Console.WriteLine("QUANT_DAFNI: DisBusFilename = " + DisBusFilename);
            Console.WriteLine("QUANT_DAFNI: DisGBRailFilename = " + DisGBRailFilename);
            Console.WriteLine("QUANT_DAFNI: GreenBeltConstraintsFilename = " + GreenBeltConstraintsFilename);
            Console.WriteLine("QUANT_DAFNI: ConstraintsBFilename = " + ConstraintsBFilename);
            Console.WriteLine("QUANT_DAFNI: PopulationTableFilename = " + PopulationTableFilename);
            Console.WriteLine("QUANT_DAFNI: ZoneCodesFilename = " + ZoneCodesFilename);

            Console.WriteLine("QUANT_DAFNI: environment variables read, now loading data");

            QUANT3ModelProperties q3props = new QUANT3ModelProperties();
            q3props.InTObs = new string[] { Path.Combine(ModelRunsDir,TObsRoadFilename), Path.Combine(ModelRunsDir,TObsBusFilename), Path.Combine(ModelRunsDir,TObsGBRailFilename) };
            q3props.Indis = new string[] { Path.Combine(ModelRunsDir,DisRoadFilename), Path.Combine(ModelRunsDir,DisBusFilename), Path.Combine(ModelRunsDir,DisGBRailFilename) };
            q3props.InPopulationFilename = Path.Combine(ModelRunsDir,PopulationTableFilename);
            q3props.InConstraints = Path.Combine(ModelRunsDir,GreenBeltConstraintsFilename);
            q3props.InOutConstraintsB = Path.Combine(ModelRunsDir, ConstraintsBFilename);
            q3props.IsUsingConstraints = true;

            Console.WriteLine("Running model calibration");
            QUANT3Model qm3 = new QUANT3Model();
            qm3.InitialiseFromProperties(q3props);
            qm3.Run(); //perform calibration step - outputs statistics.xml and three TPred_[123].bin predicted flow matrix files

            //write out predicted versions of the observed flow matrix files
            qm3.TPred[0].DirtySerialise(Path.Combine(OutputDir, TPredRoadFilename));
            qm3.TPred[1].DirtySerialise(Path.Combine(OutputDir, TPredBusFilename));
            qm3.TPred[2].DirtySerialise(Path.Combine(OutputDir, TPredGBRailFilename));

            Console.WriteLine(string.Format("Calibration complete: road beta={0}, bus beta={1}, gbrail beta={2}", qm3.Beta[0], qm3.Beta[1], qm3.Beta[2]));

            //calibration data - NOTE: all the distance matrices are in minutes, so the average trip time (CBar) is also in minutes
            float CBarObsRoad = qm3.CalculateCBar(ref qm3.TObs[0], ref qm3.dis[0]);
            float CBarPredRoad = qm3.CalculateCBar(ref qm3.TPred[0], ref qm3.dis[0]);
            float CBarObsBus = qm3.CalculateCBar(ref qm3.TObs[1], ref qm3.dis[1]);
            float CBarPredBus = qm3.CalculateCBar(ref qm3.TPred[1], ref qm3.dis[1]);
            float CBarObsGBRail = qm3.CalculateCBar(ref qm3.TObs[2], ref qm3.dis[2]);
            float CBarPredGBRail = qm3.CalculateCBar(ref qm3.TPred[2], ref qm3.dis[2]);
            Console.WriteLine(string.Format("Road: CBarObs={0} mins, CBarPred={1} mins", CBarObsRoad, CBarPredRoad));
            Console.WriteLine(string.Format("Bus: CBarObs={0} mins, CBarPred={1} mins", CBarObsBus, CBarPredBus));
            Console.WriteLine(string.Format("GBRail: CBarObs={0} mins, CBarPred={1} mins", CBarObsGBRail, CBarPredGBRail));

            Console.WriteLine("Running statistics...");
            StatisticsDataQ3 statsQ3 = new StatisticsDataQ3();
            DataTable populationTable = (DataTable)Serialiser.Get(q3props.InPopulationFilename);
            statsQ3.ComputeFromModel(qm3, ref populationTable);
            string FQStatsFilename = Path.Combine(OutputDir, StatisticsFilename);
            statsQ3.SerialiseToXML(FQStatsFilename);
            Console.WriteLine("Statistics file successfully written to " + FQStatsFilename);
            Console.WriteLine(string.Format("SorensonDice Index: road {0}, bus {1}, gbrail {2}", statsQ3.Phid[0], statsQ3.Phid[1], statsQ3.Phid[2])); //NOTE: there are other SorensonDice calculations on different variables in the file - this is Dj

            //now produce some mappable data
            Console.WriteLine("Making mappable data");
            DataTable ZoneLookup = (DataTable)Serialiser.Get(Path.Combine(ModelRunsDir, ZoneCodesFilename));
            ZoneGeocoder zg = new ZoneGeocoder(ZoneLookup);
            FMatrix[] mats = new FMatrix[] { qm3.TPred[0], qm3.TPred[1], qm3.TPred[2], qm3.TObs[0], qm3.TObs[1], qm3.TObs[2] };
            string[] fnames = new string[] { "DjRoadPred.csv", "DjBusPred.csv", "DjGBRailPred.csv", "DjRoadObs.csv", "DjBusObs.csv", "DjGBRailPred.csv" };
            for (int i = 0; i < mats.Length; i++)
            {
                float[] Dj = mats[i].ComputeDj();
                DataTable dt = zg.Geocode(Dj);
                WriteCSV(dt, Path.Combine(OutputDir, fnames[i]));
                Console.WriteLine("Written " + fnames[i]);
            }

        }
    }
}
