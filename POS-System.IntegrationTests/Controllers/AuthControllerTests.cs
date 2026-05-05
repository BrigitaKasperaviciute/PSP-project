using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class AuthControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(ApiFactory factory)
    {
        _factory = factory;
        // Auth endpoints are [AllowAnonymous] – no claims needed.
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    private static UserRegisterRequest BuildRegisterRequest(
        string? email = null,
        string? userName = null,
        string? password = "Test@1234!",
        int roleId = 2)
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        return new UserRegisterRequest(
            Email: email ?? $"user-{unique}@test.com",
            UserName: userName ?? $"user-{unique}",
            FirstName: "Test",
            LastName: "User",
            Password: password!,
            PhoneNumber: "37060000000",
            BirthDate: new DateOnly(1990, 1, 1),
            RoleId: roleId
        );
    }

    // --- Register ---

    [Fact]
    public async Task Register_WithValidPayload_ReturnsOkAndCreatesEmployee()
    {
        // Arrange
        var request = BuildRegisterRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/api/employees/register", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.Should().NotBeNull();
        body!.UserName.Should().Be(request.UserName);
        body.Email.Should().Be(request.Email);
        body.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns400()
    {
        // Arrange – register the same email twice
        var request = BuildRegisterRequest(email: $"duplicate-{Guid.NewGuid().ToString("N")[..8]}@test.com");
        await _client.PostAsJsonAsync("/api/employees/register", request);

        var duplicateRequest = BuildRegisterRequest(email: request.Email);

        // Act
        var response = await _client.PostAsJsonAsync("/api/employees/register", duplicateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error.Should().NotBeNull();
        error!.Status.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithWeakPassword_Returns400()
    {
        // Arrange – password "abc" violates Identity password policy
        var request = BuildRegisterRequest(password: "abc");

        // Act
        var response = await _client.PostAsJsonAsync("/api/employees/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- Login ---

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkAndJwtToken()
    {
        // Arrange – first register a user with a known password
        var registerRequest = BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/employees/register", registerRequest);

        var loginRequest = new UserLoginRequest(
            UserName: registerRequest.UserName,
            Password: registerRequest.Password
        );

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/login", loginRequest);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body contains a JWT token
        var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        body.Should().NotBeNull();
        body!.UserName.Should().Be(registerRequest.UserName);
        body.JwtToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        // Arrange – register a real user first
        var registerRequest = BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/employees/register", registerRequest);

        var loginRequest = new UserLoginRequest(
            UserName: registerRequest.UserName,
            Password: "WrongPassword999!"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_Returns401()
    {
        // Arrange
        var loginRequest = new UserLoginRequest(
            UserName: "doesnotexist_user_xyz",
            Password: "Test@1234!"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
