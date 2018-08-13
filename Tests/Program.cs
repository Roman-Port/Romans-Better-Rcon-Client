using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RomansRconClient2;

namespace Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            RconClient client = new RconClient(new System.Net.IPEndPoint(System.Net.IPAddress.Parse("10.0.1.13"), 32330),"ha", new RconClient.ReadyCallback((RconClient context, bool good) => {
                //We land here when we're ready
                Console.WriteLine(context.GetResponse("GetChat"));
                Console.WriteLine("Finished");
            }));

            Console.ReadLine();
        }
    }
}
