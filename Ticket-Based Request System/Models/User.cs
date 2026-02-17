using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ticket_Based_Request_System.Models
{
    public class User
    {
        public string id { get; set; } = Guid.NewGuid().ToString();
        public string employeeCode { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public string passwordHash { get; set; }
        public string role { get; set; }
        public string rolePrefix { get; set; }
        public DateTime createdAt { get; set; } = DateTime.UtcNow;
        public string profileImageUrl { get; set; }
    }
}
