using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace POS_System.IntegrationTests.Infrastructure;

internal static class IntegrationTestHelpers
{
    public static StringContent MalformedJson() => new("{", Encoding.UTF8, "application/json");

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();
        return JsonDocument.Parse(body);
    }

    public static async Task<HttpResponseMessage> PostMalformedJsonAsync(HttpClient client, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = MalformedJson()
        };

        return await client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> PutMalformedJsonAsync(HttpClient client, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = MalformedJson()
        };

        return await client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> PatchMalformedJsonAsync(HttpClient client, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = MalformedJson()
        };

        return await client.SendAsync(request);
    }

    public static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    public static async Task AssertBadRequestAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await ReadJsonAsync(response);
        json.RootElement.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetInt32().Should().Be((int)HttpStatusCode.BadRequest);
    }
}
