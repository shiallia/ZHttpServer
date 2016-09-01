using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            illidan.Server myHttpServer = new illidan.Server(9876,"d:");
            myHttpServer.Start();
        }
    }
}
