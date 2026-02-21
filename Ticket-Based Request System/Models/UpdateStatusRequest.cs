using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ticket_Based_Request_System.Models
{
    public class UpdateStatusRequest
    {
        public string ticketId { get; set; }
        public string status { get; set; }
    }
}