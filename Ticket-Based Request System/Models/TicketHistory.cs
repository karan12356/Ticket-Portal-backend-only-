using System;

namespace Ticket_Based_Request_System.Models
{
    public class TicketHistory
    {
        public string action { get; set; }         
        public string performedBy { get; set; }  
        public DateTime timestamp { get; set; } = DateTime.UtcNow;
    }
}