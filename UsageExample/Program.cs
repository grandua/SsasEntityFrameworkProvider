#define OfflineCubeFile

using System.Diagnostics;
using AgileDesign.Utilities;

namespace UsageExample
{
    public class Program
    {
        static void Main()
        {
            //Comment this line if you would like to see query results without generated MDX:
            Logger.AddTraceListener(new ConsoleTraceListener());

            (new QueryRunner()).RunQuery();
        }

    }
}
