namespace POS_System.IntegrationTests.Controllers;

public static class JsonSerializer
{
    public static string Serialize<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value);

    public static T? Deserialize<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json);

    public static T? Parse<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json);
}
