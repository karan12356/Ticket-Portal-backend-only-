using System.Collections.Generic;

namespace Ticket_Based_Request_System.Models
{
    public class BulkUpdateRequest
    {
        public List<string> ticketIds { get; set; }
        public string action { get; set; }       
        public string assignedTo { get; set; }   
    }
}
