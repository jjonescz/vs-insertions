using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace VsInsertions.Controllers;

/// <summary>
/// Used by https://github.com/jjonescz/DotNetLab.
/// </summary>
[ApiController, Route("api/cache")]
[EnableCors(corsPolicy)]
public sealed class CacheController(ILogger<CacheController> logger) : ControllerBase
{
    const string corsPolicy = "CacheControllerCorsPolicy";
    const string partitionKey = "default";

    public static void RegisterServices(IServiceCollection services)
    {
        services.AddCors(static options =>
        {
            options.AddPolicy(corsPolicy, static builder =>
            {
                builder.AllowAnyOrigin();
                builder.WithExposedHeaders("X-Timestamp");
            });
        });

        services.AddSingleton(static sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();

            return new TableClient(
                connectionString: config["Azure:Table:ConnectionString"]!,
                tableName: "Cache");
        });
    }

    [HttpPost("add/{key}")]
    public async Task<IResult> AddAsync(string key, [FromServices] TableClient tableClient)
    {
        try
        {
            var response = await tableClient.AddEntityAsync(new CacheEntry
            {
                PartitionKey = partitionKey,
                RowKey = key,
                Value = await Request.ReadBodyAsStringAsync(),
            });

            logger.LogDebug("Cached {Key}: {Response}", key, response);

            return Results.StatusCode(response.Status);
        }
        catch (RequestFailedException e)
        {
            logger.LogError(e, "Failed to cache {Key}", key);

            return Results.StatusCode(e.Status);
        }
    }

    [HttpPost("get/{key}")]
    public async Task<IResult> GetAsync(string key, [FromServices] TableClient tableClient)
    {
        var result = await tableClient.GetEntityIfExistsAsync<CacheEntry>(partitionKey, key);
        if (!result.HasValue)
        {
            logger.LogDebug("Not found {Key}", key);

            return Results.NotFound();
        }

        var value = result.Value!;

        logger.LogDebug("Found {Key}", key);

        if (value.Timestamp is { } timestamp)
        {
            Response.Headers["X-Timestamp"] = timestamp.ToString("o");
        }

        return Results.Text(value.Value);
    }
}

sealed record CacheEntry : ITableEntity
{
    public required string PartitionKey { get; set; }
    public required string RowKey { get; set; }
    public ETag ETag { get; set; } = ETag.All;
    public DateTimeOffset? Timestamp { get; set; }
    public required string Value { get; set; }
}

static class Util
{
    public static async Task<string> ReadBodyAsStringAsync(this HttpRequest request)
    {
        using var reader = new StreamReader(request.Body);
        return await reader.ReadToEndAsync();
    }
}
