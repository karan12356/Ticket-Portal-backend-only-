using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Ticket_Based_Request_System.Helpers
{
    public static class EncryptionHelper
    {
        // 32 bytes key (AES-256)
        private static readonly byte[] Key =
            Encoding.UTF8.GetBytes("12345678901234567890123456789012");

        // 16 bytes IV (AES block size)
        private static readonly byte[] IV =
            Encoding.UTF8.GetBytes("1234567890123456");

        public static string Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);

            var encryptedBytes =
                encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            return Convert.ToBase64String(encryptedBytes);
        }

        public static string Decrypt(string cipherText)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;

            using var decryptor = aes.CreateDecryptor();
            var cipherBytes = Convert.FromBase64String(cipherText);

            var decryptedBytes =
                decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(decryptedBytes);
        }
    }
}