using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection; // Add this using directive

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Load configuration from local.settings.json
builder.Configuration.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);

// Add logging
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
});

// Register Cosmos DB client
builder.Services.AddSingleton(s =>
{
    var configuration = s.GetRequiredService<IConfiguration>();
    var cosmosDbUri = configuration["CosmosDb:CosmosDB_URI"];
    var primaryKey = configuration["CosmosDb:CosmosDB_PrimaryKey"];
    return new CosmosClient(cosmosDbUri, primaryKey);
});

builder.Build().Run();
