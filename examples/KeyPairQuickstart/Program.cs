using SnowpipeStreaming;
using SnowpipeStreaming.Auth;

// Pre-req: set environment variables
//   SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PRIVATE_KEY or SNOWFLAKE_PRIVATE_KEY_PATH
//   (optional) SNOWFLAKE_PRIVATE_KEY_PASSPHRASE

var accountUrl = new Uri(Environment.GetEnvironmentVariable("SNOWFLAKE_ACCOUNT_URL") ?? "https://<account>.<region>.snowflakecomputing.com");

var provider = new EnvironmentKeyPairTokenProvider();
var client = new SnowpipeClient(accountUrl, provider);

Console.WriteLine("Discovering ingest hostname...");
var host = await client.GetHostnameAsync();
Console.WriteLine($"Ingest host: {host}");

Console.WriteLine("Exchanging scoped token...");
await client.ExchangeScopedTokenAsync(host);

Console.WriteLine("Opening channel...");
await using var channel = await client.OpenChannelAsync("DB", "SCHEMA", "PIPE", "example_channel", dropOnDispose: true);

Console.WriteLine("Appending a row...");
var next = await channel.AppendRowsAsync(new [] { new { id = 1, ts = DateTimeOffset.UtcNow } });

Console.WriteLine("Waiting for commit...");
await channel.WaitForCommitAsync(next);

Console.WriteLine("Done.");

