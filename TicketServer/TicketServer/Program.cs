using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TicketServer.Extensions;

namespace TicketServer
{
    class Program
    {

        static ConcurrentQueue<Ticket> _TicketStorage = new ConcurrentQueue<Ticket>();
        static ConcurrentBag<StreamingConnection> Connections = new ConcurrentBag<StreamingConnection>();

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
            for (int i = 0; i < (ticketCountTry ? TicketCountResult: 500); i++)
            {
                _TicketStorage.Enqueue(new Ticket(eventName, i));
            }

            // Finish line for application
            Console.WriteLine("Server is ready to process requests");



            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 11000);

            var ServerSocket = new Socket(AddressFamily.InterNetwork,
            SocketType.Stream, ProtocolType.Tcp);
            ServerSocket.Bind(localEndPoint);
            ServerSocket.Listen(1000);


            while (true)
            {
                var _client = ServerSocket.Accept();
                Task.Run(()=> { QClientWorker(_client); });
            }

        }

        private static void QClientWorker(Socket state)
        {
            var hasNewTickets = _TicketStorage.TryDequeue(out Ticket EventTicket);
            // EventTicket can be null 
            var client = new StreamingConnection(state, EventTicket);
        }
    }

    public class StreamingConnection
    {
        public readonly int ID;

        public readonly IPAddress Address;
        public readonly int Port;

        public readonly Socket Connection;

        public readonly Ticket EventTicket;
        
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
            StartConnection(true);
        }

        /// <summary>
        /// The constructor to use when the client is looking to connect to the server and asking for a ticket
        /// </summary>
        /// <param name="Address">The IP Address of the server</param>
        /// <param name="Port">The port the server socket is listening on</param>
        public StreamingConnection(IPAddress Address, int Port, int ID)
        {
            this.ID = ID;
            this.Address = Address;
            this.Port = Port;
            // Setup connection 
            Connection = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Connect()
        {
            Connection.Connect(Address, Port);

            // Setup the connection
            StartConnection(false);
        }
        

        private void StartConnection(bool TransmitTicket)
        {
            if (Connection != null && Connection.Connected)
            {
                if (TransmitTicket)
                {
                    // Doesn't require a thread to setup however will use threading to write communications to
                    _Writer = new BinaryWriter(new NetworkStream(Connection, false), System.Text.Encoding.UTF8, false);
                    TransmitEventTicketData();
                }
                else
                {
                    StartReader();
                }
            }
        }


        private void TransmitEventTicketData()
        {
            _Writer.WriteDataModel(new Communication_Model("Connection_Ticket", EventTicket));
        }

        /// <summary>
        /// This will setup the thread that will listen non-stop for data to be sent to this active connection
        /// </summary>
        private void StartReader()
        {
            try
            {
                using (var StreamReader = new BinaryReader(new NetworkStream(Connection, false), System.Text.Encoding.UTF8, false))
                {
                    while (Connection.Connected)
                    {
                        // Keep Reading and waiting
                        var result = StreamReader.ReadDataModel();

                        if (result != null)
                        {
                            var data = result.Body;
                            if (data != null)
                            {
                                Console.WriteLine($"ID {this.ID} got ticket {data.TicketID}.");
                            }
                            else
                            {
                                Console.WriteLine($"ID {ID} didn't get a ticket.");
                            }
                        }

                        throw new EndOfStreamException();
                    }
                }

            }
            catch (EndOfStreamException disconnected)
            {
                Connection.Close();
                // The user has disconnected from the stream
            }
            catch (OperationCanceledException KillThreadException)
            {
                // Get's called if the throw if canceled event hapens
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
        public int TicketID { get; set; }

        /// <summary>
        /// Event name this ticket represents
        /// </summary>
        public String EventName { get; set; }

        public Ticket(String EventName, int ID)
        {
            this.EventName = EventName;
            TicketID = ID;
        }
    }
}
