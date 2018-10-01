using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FinalAssignment
{
    class Program
    {
        // Safe way of q-ing tickets
        static ConcurrentQueue<Ticket> _TicketStorage = new ConcurrentQueue<Ticket>();
        // Safe way of storing tickets->user relations
        static ConcurrentDictionary<Ticket, float> Sales = new ConcurrentDictionary<Ticket, float>();
        // Server Socket
        static TcpListener ServerListener = new TcpListener(IPAddress.Any, 11000);
        /// <summary>
        /// Determines how many threads will be doing listening and socket connection
        /// </summary>
        const int MaxListeners = 1; // Try the extreme mode by playing with this variable, the max tickets, and the max attempts (Hint: You get some really long crunch time followed by heavy laptop fan usage)

        /// <summary>
        /// Relates to the MaxListeners this allows many threads to be q-ed and killed if needed
        /// </summary>
        static List<Task> ListenerThreads = new List<Task>();

        /// <summary>
        /// Sets how many tickets a user can buy
        /// </summary>
        static int MaxDupPurchases = 2;
        
        /// <summary>
        /// Random generator for the price calculations
        /// </summary>
        static Random priceRandomGenerator = new Random();


        /// <summary>
        /// The source of the cancel request for listener threads
        /// </summary>
        static CancellationTokenSource CancelSource = new CancellationTokenSource();

        /// <summary>
        /// The object (token) that connects a thread's death back to the source 
        /// </summary>
        static CancellationToken CancelRequestToken
        {
            get
            {
                return CancelSource.Token;
            }
        }
        static void Main(string[] args)
        {
            // Gets the event name to apply to all the tickets
            Console.Write("Enter Name of Event (Show Name): ");
            var eventName = Console.ReadLine();

            // Allows the user to set the ticket count or defaults to 500
            Console.Write("\nEnter Max Number of Tickets to be sold (Default: 500): ");
            var ticketCountTry = float.TryParse(Console.ReadLine(), out float TicketCountResult);

            // Setup how many tickets 1 user can buy before being cut off
            Console.Write("\nEnter Max Number of duplicate purchases (Default: 2): ");
            var purchaseAmountTry = int.TryParse(Console.ReadLine(), out int PurchaseAmounts);

            MaxDupPurchases = (purchaseAmountTry ? PurchaseAmounts : 2);

            // Setups up the ticket q
            Console.WriteLine("Initilizing Ticket Storage");
            for (float i = 0; i < (ticketCountTry ? TicketCountResult : 500); i++)
            {
                _TicketStorage.Enqueue(new Ticket(eventName, i, GenerateRandomPrice(0, 10.50), DateTime.Now, null));
            }

            // Starts the tcp listener
            ServerListener.Start();
            
            // This block will allow you to run multiple listeners across n threads. 1 however is more than enough for this
            // This code also checks for cancel requests
            for (int i = 0; i < MaxListeners; i++)
            {
                ListenerThreads.Add(Task.Factory.StartNew(()=> {

                    try
                    {
                        while (!CancelRequestToken.IsCancellationRequested)
                        {
                            Socket soc = ServerListener.AcceptSocket();
                            CancelRequestToken.ThrowIfCancellationRequested();
                            using (var nStream = new NetworkStream(soc))
                            {
                                using (var writer = new StreamWriter(nStream, Encoding.UTF8) { AutoFlush = true })
                                {
                                    var moreTickets = _TicketStorage.TryDequeue(out Ticket EventTicket);

                                    if (moreTickets)
                                    {
                                        EventTicket.SaleEnd = DateTime.Now;
                                    }

                                    writer.WriteLine((moreTickets ? JsonConvert.SerializeObject(EventTicket) : String.Empty));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Do nothing just hope this error wasn't deadly
                    }
                },CancelRequestToken, TaskCreationOptions.LongRunning,TaskScheduler.Current));
            }

            // Spawn all the threading ticket buyers
            StartFakeClients();

            // Cheap way of waiting for all the tickets to be consumed
            while (_TicketStorage.Count != 0) ;

            // Tells the ticket selling threads to end
            // If they actually do or not that is kinda up to them sometimes
            CancelSource.Cancel();

            // List a bunch of computation
            PrintSalesReport(Sales);

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }

        static void PrintSalesReport(ConcurrentDictionary<Ticket, float> SalesData)
        {
            // Money Formatting -- https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#the-currency-c-format-specifier
            var grps = SalesData.GroupBy(x=>x.Value);
            foreach (var grpSale in grps.OrderByDescending(x=>x.Select(y => y.Key.TicketPrice).Sum()))
            {
                Console.WriteLine($"User {grpSale.Key} purchased {grpSale.Count()} tickets for a total of {grpSale.Select(x=>x.Key.TicketPrice).Sum().ToString("C", CultureInfo.CurrentCulture)}");
            }

            var totalTickets = SalesData.Select(x => x.Key).LongCount();

            var ticketPricingQuery = SalesData.Select(x => x.Key.TicketPrice);

            var earliestValue = SalesData.OrderBy(x=>x.Key.SaleStart).Select(x=>x.Key.SaleStart).First();
            var latestValue = SalesData.Where(x=>x.Key.SaleEnd.HasValue).OrderByDescending(x => x.Key.SaleEnd).Select(x=>x.Key.SaleEnd.Value).First();

            Console.WriteLine(
$@"Revenue from ticket sales:
Sum={ticketPricingQuery.Sum().ToString("C", CultureInfo.CurrentCulture)}
Average Ticket Price={ticketPricingQuery.Average().ToString("C", CultureInfo.CurrentCulture)}
Min={ticketPricingQuery.Min().ToString("C", CultureInfo.CurrentCulture)}    Max={ticketPricingQuery.Max().ToString("C", CultureInfo.CurrentCulture)}

Time to Sell={latestValue.Subtract(earliestValue).TotalSeconds} (seconds)
1 ticket per {latestValue.Subtract(earliestValue).TotalSeconds / totalTickets} (seconds)"
);
        }

        static void StartFakeClients()
        {
            // Asks how many purchases attempts (number of buyers to create)
            Console.Write("\nEnter number of tickets to attempt to purchase (Default: 600): ");
            var attemptNumberTry = float.TryParse(Console.ReadLine(), out float attemptAmount);
            
            for (float i = 0; i < (attemptNumberTry ? attemptAmount : 600); i++)
            {
                // Fires all the clients off onto the thread pool to begin attempting to purchase 
                ThreadPool.QueueUserWorkItem(DoClientWorker, new
                {
                    ClientID = i
                });
            }
        }

        static void DoClientWorker(object state)
        {
            // Cheap way of passing in any params I want without having to cast, check types, check for anything as long as I know the property names
            dynamic paramData = state;

            int purchases = 0;
            // Cheap way of saying are tickets still available for purchase
            // Also follows the max purchase rule
            while (_TicketStorage.Count != 0 && (purchases < MaxDupPurchases))
            {
                var client = new TcpClient();
                try
                {
                    client.Connect(IPAddress.Loopback, 11000);

                    // Clean way of opening multiple streams and closing them to read data from the socket

                    using (var nStream = client.GetStream())
                    {// The stream that handles communcation over the socket
                        using (var reader = new StreamReader(nStream, Encoding.UTF8))
                        {// The stream that handles communication between the socket and converting it to strings
                            var data = reader.ReadLine();

                            if (!String.IsNullOrEmpty(data))
                            {
                                // Stores useful information for later computation
                                var ticketData = JsonConvert.DeserializeObject<Ticket>(data);
                                Sales.TryAdd(ticketData, paramData.ClientID);
                                purchases += 1;
                            }
                            else
                            {
                                Console.WriteLine($"User {paramData.ClientID} didn't get a ticket :(");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    // Just a safety pre-caution
                }
                finally
                {
                    // Making sure that the client ends the connection just in case
                    client.Close();
                } 
            }

        }

        static double GenerateRandomPrice(double minimum, double maximum)
        {
            // I needed a way to generate realistic prices
            // Source -- https://stackoverflow.com/questions/1064901/random-number-between-2-double-numbers
           
            return priceRandomGenerator.NextDouble() * (maximum - minimum) + minimum;
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
        public float TicketID { get; set; }

        /// <summary>
        /// The price of the ticket for sale
        /// </summary>
        public double TicketPrice { get; set; }

        /// <summary>
        /// Time ticket was available for sale
        /// </summary>
        public DateTime SaleStart { get; set; }

        /// <summary>
        /// Time ticket was sold
        /// </summary>
        public Nullable<DateTime> SaleEnd { get; set; }

        /// <summary>
        /// Event name this ticket represents
        /// </summary>
        public String EventName { get; set; }

        public Ticket(String EventName, float ID, double Price, DateTime SaleStart, Nullable<DateTime> SaleEnd)
        {
            this.EventName = EventName;
            TicketID = ID;
            TicketPrice = Price;
            this.SaleStart = SaleStart;
            this.SaleEnd = SaleEnd;
        }
    }
}
