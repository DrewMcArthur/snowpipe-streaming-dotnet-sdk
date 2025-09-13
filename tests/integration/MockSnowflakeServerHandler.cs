using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Integration.Tests;

public sealed class MockSnowflakeServerHandler : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes = new();
    private string? _lastScopedToken;

    public void Map(string method, string path, Func<HttpRequestMessage, HttpResponseMessage> responder)
        => MapHost("*", method, path, responder);

    public void MapHost(string host, string method, string path, Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var key = $"{method.ToUpperInvariant()} {host} {path}";
        _routes[key] = responder;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        // Enforce presence of User-Agent header
        if (request.Headers.UserAgent == null || !request.Headers.UserAgent.Any())
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Missing User-Agent header")
            });
        }
        // Enforce specific endpoint semantics
        if (string.Equals(request.RequestUri!.AbsolutePath, "/oauth/token", StringComparison.OrdinalIgnoreCase))
        {
            var ct = request.Content?.Headers.ContentType?.MediaType;
            if (!string.Equals(ct, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Content-Type must be application/x-www-form-urlencoded")
                });
            }
        }
        if (request.RequestUri!.AbsolutePath.EndsWith("/rows", StringComparison.OrdinalIgnoreCase))
        {
            var ct = request.Content?.Headers.ContentType?.MediaType;
            if (!string.Equals(ct, "application/x-ndjson", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Content-Type must be application/x-ndjson")
                });
            }
            var q = ParseQuery(request.RequestUri!);
            if (!q.ContainsKey("continuationToken"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Missing continuationToken query parameter")
                });
            }
        }
        var method = request.Method.Method.ToUpperInvariant();
        var path = request.RequestUri!.AbsolutePath;
        var auth = request.Headers.Authorization;
        bool isAccountEndpoint = string.Equals(path, "/v2/streaming/hostname", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(path, "/oauth/token", StringComparison.OrdinalIgnoreCase);
        if (isAccountEndpoint)
        {
            if (auth is null || !string.Equals(auth.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(auth.Parameter))
            {
                return Task.FromResult(Json(HttpStatusCode.Unauthorized, new { error_code = "UNAUTHORIZED", message = "Missing or invalid JWT bearer token", requestId = Guid.NewGuid().ToString() }));
            }
        }
        else
        {
            if (auth is null || !string.Equals(auth.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(auth.Parameter))
            {
                return Task.FromResult(Json(HttpStatusCode.Unauthorized, new { error_code = "UNAUTHORIZED", message = "Missing scoped bearer token", requestId = Guid.NewGuid().ToString() }));
            }
            if (_lastScopedToken is null || !string.Equals(auth.Parameter, _lastScopedToken, StringComparison.Ordinal))
            {
                return Task.FromResult(Json(HttpStatusCode.Unauthorized, new { error_code = "UNAUTHORIZED", message = "Invalid scoped bearer token", requestId = Guid.NewGuid().ToString() }));
            }
        }
        var host = request.RequestUri!.Authority;
        var specific = $"{method} {host} {path}";
        var wildcard = $"{method} * {path}";
        if (_routes.TryGetValue(specific, out var responder) || _routes.TryGetValue(wildcard, out responder))
        {
            var response = responder(request);
            if (isAccountEndpoint && string.Equals(path, "/oauth/token", StringComparison.OrdinalIgnoreCase) && (int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
            {
                try
                {
                    var json = response.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (!string.IsNullOrEmpty(json))
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("token", out var t) && t.ValueKind == JsonValueKind.String)
                        {
                            _lastScopedToken = t.GetString();
                        }
                    }
                }
                catch { }
            }
            return Task.FromResult(response);
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"No route for {specific} or {wildcard}")
        });
    }

    public static HttpResponseMessage Json(HttpStatusCode status, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    public static IDictionary<string, string> ParseQuery(Uri uri)
    {
        var query = HttpUtility.ParseQueryString(uri.Query);
        return query.AllKeys!.ToDictionary(k => k!, k => query[k!]!);
    }
}
