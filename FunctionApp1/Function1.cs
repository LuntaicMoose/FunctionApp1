using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;

namespace FunctionApp1
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;

        public Function1(ILogger<Function1> logger, IConfiguration configuration)
        {
            _logger = logger;

            var cosmosDbUri = configuration["CosmosDb:CosmosDB_URI"];
            var primaryKey = configuration["CosmosDb:CosmosDB_PrimaryKey"];
            var databaseId = configuration["CosmosDb:CosmosDB_DatabaseId"];
            var containerId = configuration["CosmosDb:CosmosDB_ContainerId"];

            _cosmosClient = new CosmosClient(cosmosDbUri, primaryKey);
            _container = _cosmosClient.GetContainer(databaseId, containerId);
        }

        [Function("Function1")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string directoryPath = @"C:\Users\earan\source\repos\FunctionApp1\FunctionApp1\appinsights_backup\";
            var jsonFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);

            foreach (var file in jsonFiles)
            {
                try
                {
                    using (var reader = new StreamReader(file))
                    {
                        string? content = await reader.ReadToEndAsync();
                        // Fix the JSON format by ensuring each object is separated by a comma and wrapped in an array
                        content = "[" + content.Replace("}\r\n{", "},{") + "]";
                        var jsonObjects = JArray.Parse(content);

                        foreach (var jsonObject in jsonObjects)
                        {
                            // Ensure each JSON object has an 'id' property
                            if (jsonObject["id"] == null)
                            {
                                jsonObject["id"] = Guid.NewGuid().ToString();
                            }
                            await _container.CreateItemAsync(jsonObject);
                        }
                    }
                }
                catch (JsonReaderException ex)
                {
                    _logger.LogError(ex, $"Error reading JSON from file: {file}");
                    return new StatusCodeResult(StatusCodes.Status400BadRequest);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing file: {file}");
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
            }

            return new OkObjectResult("JSON files processed and data added to Cosmos DB.");
        }


        [Function("QueryCosmosDB")]
        public async Task<IActionResult> QueryCosmosDB([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a query request.");

            if (!req.Query.Any())
            {
                return new BadRequestObjectResult("Please provide at least one key to query.");
            }

            var query = new QueryDefinition("SELECT c FROM c");
            var iterator = _container.GetItemQueryIterator<JObject>(query);
            var results = new List<JObject>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                foreach (var item in response)
                {
                    bool matches = true;

                    foreach (var queryParam in req.Query)
                    {
                        var key = queryParam.Key;
                        var value = queryParam.Value.ToString();
                        var itemValue = GetValueByKey(item, key);

                        if (itemValue == null || !itemValue.Contains(value, StringComparison.OrdinalIgnoreCase))
                        {
                            matches = false;
                            break;
                        }
                    }

                    if (matches)
                    {
                        results.Add(item);
                    }
                }
            }

            return new OkObjectResult(results);
        }

        private string? GetValueByKey(JObject item, string key)
        {
            foreach (var property in item.Properties())
            {
                if (property.Name == key)
                {
                    return property.Value.ToString();
                }
                else if (property.Value.Type == JTokenType.Object)
                {
                    var nestedValue = GetValueByKey((JObject)property.Value, key);
                    if (nestedValue != null)
                    {
                        return nestedValue;
                    }
                }
            }
            return null;
        }
    }
}
