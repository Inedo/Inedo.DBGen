using System;
using System.IO;
using Inedo.Data.CodeGenerator.Properties;

namespace Inedo.Data.CodeGenerator
{
    public static class Program
    {
        private static readonly ConsoleColor DefaultColor = Console.ForegroundColor;

        public static void Main(string[] args)
        {
            var connect = SqlServerConnection.Connect;
            var connectionString = Settings.Default.ConnectionString;
            var baseNamespace = Settings.Default.BaseNamespace;
            var dataFactoryType = Settings.Default.DataFactoryType;
            var sprocPrefix = Settings.Default.StoredProcInfoPrefix;
            var dataContextType = Settings.Default.DataContextType;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                WriteErrorMessage("ConnectionString not set in config file.");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(baseNamespace))
            {
                WriteErrorMessage("BaseNamespace not set in config file.");
                return;
            }

            if (string.IsNullOrWhiteSpace(dataFactoryType))
            {
                WriteErrorMessage("DataFactoryType not set in config file.");
                return;
            }

            var gens = new CodeGeneratorBase[]
            { 
                new SqlContextGenerator(connect, connectionString, baseNamespace, dataContextType, sprocPrefix),
                new SqlTableGenerator(connect, connectionString, baseNamespace),
                new SqlEventTypesGenerator(connect, connectionString, baseNamespace)
            };

            foreach (var gen in gens)
            {
                if (!gen.ShouldGenerate())
                {
                    Console.WriteLine("Skipping " + gen.FileName);
                    continue;
                }
                var fileName = gen.FileName;
                if (TryOpenFile(fileName, out FileStream fs))
                {
                    gen.GenerateCodeFile(fs);
                    WriteInfoMessage(fileName);
                }
                else
                {
                    WriteFileAccessErrorMessage(fileName);
                }
            }
        }

        private static void WriteErrorMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = DefaultColor;
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        private static void WriteFileAccessErrorMessage(string fileName)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\"{0}\" cannot be opened for writing.", fileName);
            Console.WriteLine("It is possible that the file is marked \"read-only\" and needs to be checked-out from source control.");
            Console.ForegroundColor = DefaultColor;
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        private static void WriteInfoMessage(string fileName)
        {
            Console.WriteLine("\"{0}\" was created successfully.", fileName);
            Console.WriteLine();
        }
        private static bool TryOpenFile(string path, out FileStream stream)
        {
            try
            {
                stream = File.Open(path, FileMode.Create, FileAccess.Write);
                return true;
            }
            catch
            {
                stream = null;
                return false;
            }
        }
    }
}
