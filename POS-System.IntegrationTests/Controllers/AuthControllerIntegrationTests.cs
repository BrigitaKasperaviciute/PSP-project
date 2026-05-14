using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class AuthControllerIntegrationTests
{
    [Fact]
    public async Task RegisterUser_ValidCredentials_ReturnsOkAndKeepsUserState()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userName = "integration-user-" + Guid.NewGuid().ToString("N")[..8];
        var email = $"{userName}@example.com";
        const string password = "P@ssw0rd!";

        // Arrange
        var request = new
        {
            Email = email,
            UserName = userName,
            FirstName = "New",
            LastName = "User",
            Password = password,
            PhoneNumber = "1234567890",
            BirthDate = "1990-01-01",
            RoleId = 1
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/employees/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<object>();
        body.Should().NotBeNull();

        var exists = await factory.ExecuteDbContextAsync(db => db.Users.AnyAsync(x => x.UserName == userName));
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithJwtToken()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Arrange - register user
        var userName = "loginuser-" + Guid.NewGuid().ToString("N")[..8];
        var register = new
        {
            Email = $"{userName}@test.com",
            UserName = userName,
            FirstName = "Login",
            LastName = "Test",
            Password = "Test@1234!",
            PhoneNumber = "1234567890",
            BirthDate = "1990-01-01",
            RoleId = 1
        };
        await client.PostAsJsonAsync("/api/employees/register", register);

        var loginRequest = new { UserName = register.UserName, Password = "Test@1234!" };

        // Act
        var response = await client.PostAsJsonAsync("/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("jwtToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RegisterUser_InvalidJson_ReturnsBadRequestAndKeepsDatabaseUnchanged()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Users.CountAsync());

        // Act
        var response = await IntegrationTestHelpers.PostMalformedJsonAsync(client, "/api/employees/register");

        // Assert
        await IntegrationTestHelpers.AssertBadRequestAsync(response);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Users.CountAsync());
        countAfter.Should().Be(countBefore);
    }
}
