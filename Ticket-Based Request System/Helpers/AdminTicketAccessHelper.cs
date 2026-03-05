using System.Security.Cryptography;
using System.Text;
using Ticket_Based_Request_System.Models;

namespace Ticket_Based_Request_System.Helpers
{
    public static class AdminTicketAccessHelper
    {
        public static string GenerateReadablePass(Ticket ticket)
        {
            var words = ticket.title.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            string initials = string.Join("", words.Select(w => w[0])).ToUpper();

            string date = DateTime.Now.ToString("yyyyMMdd");

            return $"{initials}-{date}";
        }

        public static string GenerateHash(string value)
        {
            using var sha = SHA256.Create();

            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));

            return Convert.ToHexString(bytes);
        }

        public static bool Validate(Ticket ticket, string userPass)
        {
            var expectedReadable = GenerateReadablePass(ticket);

            var expectedHash = GenerateHash(expectedReadable);

            var providedHash = GenerateHash(userPass);

            return expectedHash == providedHash;
        }
    }
}