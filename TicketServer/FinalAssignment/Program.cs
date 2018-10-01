using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FinalAssignment
{
    class Program
    {
        static ConcurrentQueue<Ticket> _TicketStorage = new ConcurrentQueue<Ticket>();
        static Socket ServerSocket;

        static void Main(string[] args)
        {
            Console.WriteLine("Starting Server ...");

            // Gets the event name to apply to all the tickets
            Console.Write("Enter Name of Event: ");
            var eventName = Console.ReadLine();

            // Allows the user to set the ticket count or defaults to 500
            Console.Write("\nEnter Max Number of Tickets (Default: 500): ");
            var ticketCountTry = int.TryParse(Console.ReadLine(), out int TicketCountResult);

            // Setups up the ticket q
            Console.WriteLine("Initilizing Ticket Storage");
            for (int i = 0; i < (ticketCountTry ? TicketCountResult : 500); i++)
            {
                _TicketStorage.Enqueue(new Ticket(eventName, i));
            }

            // Finish line for application
            Console.WriteLine("Server is ready to process requests");


        }


        static void StartServerListener()
        {
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 11000);

            ServerSocket = new Socket(AddressFamily.InterNetwork,
            SocketType.Stream, ProtocolType.Tcp);
            ServerSocket.Bind(localEndPoint);
            ServerSocket.Listen(100);

            while (true)
            {
                
            }
        }
    }

    /// <summary>
    /// A structure for storing information about an event
    /// </summary>
    public class Ticket
    {
        /// <summary>
        /// Unique identifer for the ticket 
        /// </summary>
        public readonly int TicketID;

        /// <summary>
        /// Event name this ticket represents
        /// </summary>
        public readonly String EventName;

        public Ticket(String EventName, int ID)
        {
            this.EventName = EventName;
            TicketID = ID;
        }
    }
}
