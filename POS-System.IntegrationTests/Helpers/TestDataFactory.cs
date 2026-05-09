using System.Text.Json;
using System.Text;

namespace POS_System.IntegrationTests.Helpers;

public static class TestDataFactory
{
    public static StringContent ToJsonContent<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
