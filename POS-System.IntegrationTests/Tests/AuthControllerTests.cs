using System.Net;
using System.Net.Http.Json;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Fixtures;

namespace POS_System.IntegrationTests.Tests;

[Collection("Integration")]
public class AuthControllerTests(PosWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_ValidRequest_ReturnsOk()
    {
        var request = new UserRegisterRequest(
            Email: $"test_{Guid.NewGuid():N}@example.com",
            UserName: $"user_{Guid.NewGuid():N}",
            FirstName: "Test",
            LastName: "User",
            Password: "Test@1234",
            PhoneNumber: "1234567890",
            BirthDate: new DateOnly(1990, 1, 1),
            RoleId: 0);

        var response = await _client.PostAsJsonAsync("/api/employees/register", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var request = new UserLoginRequest("nonexistent_user_xyz", "WrongPass@1");

        var response = await _client.PostAsJsonAsync("/v1/auth/login", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOk()
    {
        var uniqueUser = $"logintest_{Guid.NewGuid():N}";
        var email = $"{uniqueUser}@example.com";
        var registerReq = new UserRegisterRequest(email, uniqueUser, "A", "B", "Test@1234", "1234567890", new DateOnly(1990, 1, 1), 0);
        await _client.PostAsJsonAsync("/api/employees/register", registerReq);

        var loginReq = new UserLoginRequest(uniqueUser, "Test@1234");
        var response = await _client.PostAsJsonAsync("/v1/auth/login", loginReq);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
