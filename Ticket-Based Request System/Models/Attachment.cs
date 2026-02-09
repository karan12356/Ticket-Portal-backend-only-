using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ticket_Based_Request_System.Models
{
    public class Attachment
    {
        public string fileName { get; set; }
        public string fileType { get; set; }
        public string fileUrl { get; set; }
        public DateTime uploadedAt { get; set; }
    }
}
