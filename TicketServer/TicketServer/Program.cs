using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using TicketServer.Extensions;

namespace TicketServer
{
    class Program
    {

        static ConcurrentQueue<Ticket> _TicketStorage = new ConcurrentQueue<Ticket>();

        static void Main(string[] args)
        {
            Console.WriteLine("Starting Server ...");

            // Gets the event name to apply to all the tickets
            Console.Write("Enter Name of Event: ");
            var eventName = Console.ReadLine();

            // Allows the user to set the ticket count or defaults to 500
            Console.Write("\nEnter Max Number of Tickets (Default: 500): ");
            var ticketCountTry = int.TryParse(Console.ReadLine(), out int TicketCountResult);

            // Allows tickets to re-enter the q if the user disconnects
            Console.WriteLine("Allow ticket resale? Y\\N");
            var AllowResale = ("y".Equals(Console.ReadLine().Trim().ToLower(), StringComparison.OrdinalIgnoreCase));

            // Setups up the ticket q
            Console.WriteLine("Initilizing Ticket Storage");
            for (int i = 0; i < (ticketCountTry ? TicketCountResult: 500); i++)
            {
                _TicketStorage.Enqueue(new Ticket(eventName));
            }

            // Finish line for application
            Console.WriteLine("Server is ready to process requests");


            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();

        }
    }

    class StreamingConnection
    {
        public readonly Socket Connection;

        public readonly Ticket EventTicket;

        public Task Reader;
        private BinaryWriter _Writer;

        /// <summary>
        /// The constructor to use when the server is creating a reference to a new client
        /// </summary>
        /// <param name="Connection">The socket to the client</param>
        /// <param name="EventTicket">The ticket the client will get, can be null</param>
        public StreamingConnection(Socket Connection, Ticket EventTicket)
        {
            this.EventTicket = EventTicket;

            this.Connection = Connection;

            // Setup the reader to accept communication from a client to the server
            // Start this reader from the thread pool because it may or may not be long running 
            // Because a client could stay connected for a long time or a very short time
            StartConnection(TaskCreationOptions.PreferFairness);
        }

        /// <summary>
        /// The constructor to use when the client is looking to connect to the server and asking for a ticket
        /// </summary>
        /// <param name="Address">The IP Address of the server</param>
        /// <param name="Port">The port the server socket is listening on</param>
        public StreamingConnection(IPAddress Address, int Port)
        {
            // Setup connection 
            Connection = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Connection.Connect(Address, Port);

            // Setup the connection
        }

        private void StartConnection(TaskCreationOptions ThreadOptions)
        {
            if (Connection != null)
            {
                
                StartReader(ThreadOptions);

                
                // Doesn't require a thread to setup however will use threading to write communications to
                _Writer = new BinaryWriter(new NetworkStream(Connection, false), System.Text.Encoding.UTF8,false);
                
            }
        }

        /// <summary>
        /// This will setup the thread that will listen non-stop for data to be sent to this active connection
        /// </summary>
        /// <param name="ThreadLocation">Determine if the thread comes from the thread pool or a new thread</param>
        private void StartReader(TaskCreationOptions ThreadLocation)
        {
            Reader = Task.Factory.StartNew(()=> 
            {
                try
                {
                    using (var StreamReader = new BinaryReader(new NetworkStream(Connection, false), System.Text.Encoding.UTF8, false))
                    {
                        while (Connection.Connected)
                        {
                            // ThrowIfCanceled
                            // Keep Reading and waiting
                            var result = StreamReader.ReadDataModel();
                            //MessageEvent?.Invoke(this, result);


                            // Go back to waiting for content
                        }
                    }

                }
                catch (EndOfStreamException disconnected)
                {
                    Connection.Close();
                    // The user has disconnected from the stream
                    //ConnectionEvent?.Invoke(this, true);
                }
                catch (OperationCanceledException KillThreadException)
                {
                    // Get's called if the throw if canceled event hapens
                }
            }, ThreadLocation);
        }
    }

    

    /// <summary>
    /// A structure for storing information about an event
    /// </summary>
    class Ticket
    {
        /// <summary>
        /// Unique identifer for the ticket 
        /// </summary>
        public readonly Guid TicketID;

        /// <summary>
        /// Event name this ticket represents
        /// </summary>
        public readonly String EventName;

        public Ticket(String EventName)
        {
            this.EventName = EventName;
            TicketID = Guid.NewGuid();
        }
    }
}
