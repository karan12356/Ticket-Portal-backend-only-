using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Net.Http.Headers;
using System.Net;
using Ticket_Based_Request_System.Helpers;
using Ticket_Based_Request_System.Models;
using Ticket_Based_Request_System.Services;

namespace Ticket_Based_Request_System.Functions.Tickets
{
    public class CreateTicket
    {
        private readonly CosmosDbService _cosmos;
        private readonly BlobStorageService _blob;

        public CreateTicket(CosmosDbService cosmos, BlobStorageService blob)
        {
            _cosmos = cosmos;
            _blob = blob;
        }

        [Function("CreateTicket")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tickets")]
            HttpRequestData req)
        {
            try
            {
                if (!req.Headers.TryGetValues("Content-Type", out var values) ||
                    !values.First().StartsWith("multipart/form-data"))
                {
                    return BadRequest(req, "Content-Type must be multipart/form-data");
                }

                string boundary = HeaderUtilities.RemoveQuotes(
                    MediaTypeHeaderValue.Parse(values.First()).Boundary
                ).Value;

                var reader = new MultipartReader(boundary, req.Body);

                string userId = null, employeeCode = null, role = null,
                       rolePrefix = null, title = null, description = null,
                       category = null, requestType = "General";

                bool isDraft = false;
                bool isConfidential = false;

                var attachments = new List<Attachment>();
                var pendingFiles = new List<(MultipartSection section, string fileName)>();

                MultipartSection section;
                while ((section = await reader.ReadNextSectionAsync()) != null)
                {
                    if (string.IsNullOrEmpty(section.ContentDisposition))
                        continue;

                    var contentDisposition = ContentDispositionHeaderValue.Parse(section.ContentDisposition);

                    if (contentDisposition.IsFormDisposition())
                    {
                        using var sr = new StreamReader(section.Body);
                        var value = await sr.ReadToEndAsync();

                        switch (contentDisposition.Name.Value)
                        {
                            case "userId": userId = value; break;
                            case "employeeCode": employeeCode = value; break;
                            case "role": role = value; break;
                            case "rolePrefix": rolePrefix = value; break;
                            case "title": title = value; break;
                            case "description": description = value; break;
                            case "category": category = value; break;
                            case "requestType": requestType = value; break;
                            case "isDraft": bool.TryParse(value, out isDraft); break;
                            case "isConfidential": bool.TryParse(value, out isConfidential); break;
                        }
                    }
                    else if (contentDisposition.IsFileDisposition())
                    {
                        pendingFiles.Add((section, contentDisposition.FileName.Value));
                    }
                }

                if (string.IsNullOrWhiteSpace(userId) ||
                    string.IsNullOrWhiteSpace(employeeCode) ||
                    string.IsNullOrWhiteSpace(role) ||
                    string.IsNullOrWhiteSpace(rolePrefix) ||
                    string.IsNullOrWhiteSpace(title) ||
                    string.IsNullOrWhiteSpace(category))
                {
                    return BadRequest(req, "Missing required fields");
                }

                // Handle attachments
                foreach (var (fileSection, fileName) in pendingFiles)
                {
                    var ext = Path.GetExtension(fileName).ToLower();
                    if (ext != ".jpg" && ext != ".jpeg" && ext != ".pdf")
                        return BadRequest(req, "Only JPG, JPEG, PDF allowed");

                    string blobPath = $"tickets/{userId}/{Guid.NewGuid()}_{fileName}";
                    string fileUrl = await _blob.UploadAsync(blobPath, fileSection.Body, fileSection.ContentType);

                    attachments.Add(new Attachment
                    {
                        fileName = fileName,
                        fileType = fileSection.ContentType,
                        fileUrl = fileUrl,
                        uploadedAt = DateTime.UtcNow
                    });
                }

                string confirmationNumber = null;
                DateTime? submittedAt = null;

                // 🔥 SAFE COUNTER HANDLING
                if (!isDraft)
                {
                    int nextNumber = 1;

                    try
                    {
                        var counterResponse = await _cosmos.Counters.ReadItemAsync<dynamic>(
                            rolePrefix,
                            new PartitionKey("ticket"));

                        nextNumber = counterResponse.Resource.currentValue + 1;
                        counterResponse.Resource.currentValue = nextNumber;

                        await _cosmos.Counters.ReplaceItemAsync(
                            counterResponse.Resource,
                            rolePrefix,
                            new PartitionKey("ticket"));
                    }
                    catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        var newCounter = new
                        {
                            id = rolePrefix,
                            currentValue = 1
                        };

                        await _cosmos.Counters.CreateItemAsync(
                            newCounter,
                            new PartitionKey("ticket"));

                        nextNumber = 1;
                    }

                    confirmationNumber = $"{rolePrefix}-{nextNumber:D5}";
                    submittedAt = DateTime.UtcNow;
                }

                var ticket = new Ticket
                {
                    confirmationNumber = confirmationNumber,
                    userId = userId,
                    employeeCode = employeeCode,
                    role = role,
                    requestType = requestType,
                    title = title,
                    description = isConfidential
                        ? EncryptionHelper.Encrypt(description)
                        : description,
                    category = category,
                    status = isDraft ? "Draft" : "Open",
                    isDraft = isDraft,
                    submittedAt = submittedAt,
                    isConfidential = isConfidential,
                    attachments = attachments,
                    createdAt = DateTime.UtcNow,
                    updatedAt = DateTime.UtcNow
                };

                await _cosmos.Tickets.CreateItemAsync(ticket, new PartitionKey(userId));

                var res = req.CreateResponse(HttpStatusCode.Created);
                await res.WriteAsJsonAsync(ticket);
                return res;
            }
            catch (Exception ex)
            {
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteStringAsync(ex.Message);
                return err;
            }
        }

        private HttpResponseData BadRequest(HttpRequestData req, string msg)
        {
            var res = req.CreateResponse(HttpStatusCode.BadRequest);
            res.WriteString(msg);
            return res;
        }
    }
}