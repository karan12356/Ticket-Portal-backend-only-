using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ticket_Based_Request_System.Models
{
    public class ViewTicketRequest
    {
        public string UserId { get; set; }
        public string Role { get; set; }
        public string AdminUserId { get; set; }
        public string Password { get; set; }
    }
}
