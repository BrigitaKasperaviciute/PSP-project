using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace POS_System.IntegrationTests.Infrastructure;

public static class IntegrationTestHelpers
{
    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    public static async Task AssertBadRequestAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNull();
    }

    public static async Task<HttpResponseMessage> PostMalformedJsonAsync(HttpClient client, string path)
    {
        var content = new StringContent("{invalid-json}", System.Text.Encoding.UTF8, "application/json");
        return await client.PostAsync(path, content);
    }
}
