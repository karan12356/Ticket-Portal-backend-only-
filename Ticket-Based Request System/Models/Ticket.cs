using System;
using System.Collections.Generic;

namespace Ticket_Based_Request_System.Models
{
    public class Ticket
    {
        public string id { get; set; } = Guid.NewGuid().ToString();

        public string confirmationNumber { get; set; }
        public string userId { get; set; }
        public string employeeCode { get; set; }
        public string role { get; set; }

        public string requestType { get; set; } = "General";

        public string title { get; set; }
        public string description { get; set; }
        public string category { get; set; }

        public string status { get; set; } = "Open";
        public bool isDraft { get; set; } = false;

        public string assignedTo { get; set; }

        public DateTime? submittedAt { get; set; }
        public bool isConfidential { get; set; } = false;
        public object adminData { get; set; }

        public List<Attachment> attachments { get; set; } = new();

        public DateTime createdAt { get; set; } = DateTime.UtcNow;
        public DateTime updatedAt { get; set; } = DateTime.UtcNow;
    }
}