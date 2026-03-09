using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ticket_Based_Request_System.Repositories;
using Ticket_Based_Request_System.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton<CosmosDbService>();
builder.Services.AddSingleton<BlobStorageService>();
builder.Services.AddSingleton<CosmosDbService>();

builder.Services.AddSingleton<CosmosRepository>();

builder.Services.AddSingleton<BlobRepository>();

builder.Services.AddSingleton<BlobStorageService>();

builder.Build().Run();
