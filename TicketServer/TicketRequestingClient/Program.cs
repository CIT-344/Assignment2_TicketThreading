using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TicketServer;

namespace TicketRequestingClient
{
    class Program
    {
        static void Main(string[] args)
        {

            ThreadPool.SetMaxThreads(500, 150);
            List<Task> Workers = new List<Task>();
            

            Console.Write("Enter number of tickets to attempt to purchase (Default: 600): ");
            var attemptNumberTry = int.TryParse(Console.ReadLine(), out int attemptAmount);

            // Setup all the connections to be in the ready state
            for (int i = 0; i < (attemptNumberTry ? attemptAmount : 600); i++)
            {
                var con = new StreamingConnection(IPAddress.Loopback, 11000, i);
                var worker = Task.Run(() =>
                {
                    con.Connect();
                });
                Workers.Add(worker);
            }


            Task.WaitAll(Workers.ToArray());

            Console.ReadKey();
        }
    }
}
