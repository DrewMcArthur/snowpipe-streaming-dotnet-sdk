using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace SnowpipeStreaming.Internal;

internal sealed class HttpSnowflakeTransport
{
    private readonly HttpClient _http;
    private readonly ILogger? _logger;

    public HttpSnowflakeTransport(HttpClient http, ILogger? logger)
    {
        _http = http;
        _logger = logger;
    }
}

