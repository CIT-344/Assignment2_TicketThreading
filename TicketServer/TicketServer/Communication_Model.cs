using System;

namespace TicketServer
{
    public class Communication_Model
    {
        public readonly String EventName;
        public readonly DateTime DateReceived;
        public readonly Ticket Body;

        public Communication_Model(String EventName, Ticket Body)
        {
            this.EventName = EventName;
            this.DateReceived = DateTime.Now;
            this.Body = Body;
        }
    }
}
