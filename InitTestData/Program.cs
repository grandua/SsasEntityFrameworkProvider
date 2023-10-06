using System;
using SsasEntityFrameworkProvider.AcceptanceTests;

namespace InitTestData
{
    class Program
    {
        /// <summary>
        /// Also run ModelExampleCube.xmla (\AdomdExtensions.AcceptanceTests\AppData\ModelExampleCube.xmla)
        /// and CreateNorthwindEFDB.sql (\3rdParty\Source\EFSampleMsSqServerlProvider\EFSampleProvider\NorthwindEFModel\Database\CreateNorthwindEFDB.sql)
        /// </summary>
        static void Main()
        {
            var testDataInitializer = new DbContextAcceptance();
            testDataInitializer.PersistReportLineAllPrimitiveDataTypesTest();

            WriteSuccessToConsole();
        }

        static void WriteSuccessToConsole()
        {
            Console.WriteLine("Success");
            Console.WriteLine(@"Also run ModelExampleCube.xmla (\AdomdExtensions.AcceptanceTests\AppData\ModelExampleCube.xmla)");
            Console.WriteLine(@"and CreateNorthwindEFDB.sql (\3rdParty\Source\EFSampleMsSqServerlProvider\EFSampleProvider\NorthwindEFModel\Database\CreateNorthwindEFDB.sql)");
            Console.WriteLine();
            Console.Write("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
