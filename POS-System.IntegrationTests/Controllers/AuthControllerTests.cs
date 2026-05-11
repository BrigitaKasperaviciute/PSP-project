using FluentAssertions;
using Microsoft.AspNetCore.Identity.Data;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class AuthControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        // Use the factory's test client (in-memory TestServer, not real HTTP)
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    #region Happy Path Tests

    [Fact]
    public async Task RegisterUser_WithValidPayload_ReturnsOkWithUserResponse()
    {
        // Arrange
        var request = new UserRegisterRequestBuilder()
            .WithEmail($"testuser-{Guid.NewGuid():N}@example.com")
            .WithPassword("Test@Password123")
            .WithFirstName("Test")
            .WithLastName("User")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/employees/register", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<UserRegisterResponse>();
        body.Should().NotBeNull();
        body!.Email.Should().Be(request.Email);
    }

    [Fact]
    public async Task LoginUser_WithValidCredentials_ReturnsOkWithToken()
    {
        // Arrange - first register a user
        var email = $"logintest-{Guid.NewGuid():N}@example.com";
        var password = "Test@Password123";
        var registerRequest = new UserRegisterRequestBuilder()
            .WithEmail(email)
            .WithPassword(password)
            .Build();

        var registerResponse = await _client.PostAsJsonAsync("/api/employees/register", registerRequest);
        if (registerResponse.StatusCode != HttpStatusCode.OK)
        {
            var text = await registerResponse.Content.ReadAsStringAsync();
            throw new Exception($"Registration failed {(int)registerResponse.StatusCode}: {text}");
        }

        // Act - login with those credentials (use the registered UserName)
        var loginRequest = new UserLoginRequest(registerRequest.UserName, password);
        var response = await _client.PostAsJsonAsync("/v1/auth/login", loginRequest);

        // If not OK, include response body for debugging
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            throw new Exception($"Unexpected status {(int)response.StatusCode}: {errorText}");
        }

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<POS_System.Business.Dtos.Response.UserLoginResponse>();
        body.Should().NotBeNull();
        body!.JwtToken.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Password Recovery Tests

    [Fact]
    public async Task ForgotPassword_WithUnknownEmail_ReturnsOk()
    {
        // Arrange
        var request = new ForgotPasswordRequest
        {
            Email = $"unknown-{Guid.NewGuid():N}@example.com"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/forgot-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_WithUnknownEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = $"unknown-{Guid.NewGuid():N}@example.com",
            ResetCode = "invalid-code",
            NewPassword = "Test@Password123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/reset-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Negative Flow Tests

    [Fact]
    public async Task RegisterUser_WithDuplicateEmail_ReturnsBadRequestOrConflict()
    {
        // Arrange
        var email = $"duplicate-{Guid.NewGuid():N}@example.com";
        var request = new UserRegisterRequestBuilder().WithEmail(email).Build();

        // Register first user
        await _client.PostAsJsonAsync("/api/employees/register", request);

        // Act - try to register with same email
        var response = await _client.PostAsJsonAsync("/api/employees/register", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RegisterUser_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = """{"email": "invalid-email", "password": "Test@Password123", "firstName": "Test", "lastName": "User"}""";

        // Act
        var response = await _client.PostAsJsonAsync("/api/employees/register", invalidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterUser_WithWeakPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new UserRegisterRequestBuilder()
            .WithPassword("weak")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/employees/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LoginUser_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var loginRequest = new UserLoginRequest("nonexistent@example.com", "Test@Password123");

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginUser_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange - register a user first
        var email = $"wrongpwd-{Guid.NewGuid():N}@example.com";
        var registerRequest = new UserRegisterRequestBuilder()
            .WithEmail(email)
            .WithPassword("Correct@Password123")
            .Build();

        await _client.PostAsJsonAsync("/api/employees/register", registerRequest);

        // Act - login with wrong password
        var loginRequest = new UserLoginRequest(email, "Wrong@Password123");
        var response = await _client.PostAsJsonAsync("/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterUser_WithNullEmail_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = """{"email": null, "password": "Test@Password123", "firstName": "Test", "lastName": "User"}""";

        // Act
        var response = await _client.PostAsJsonAsync("/api/employees/register", invalidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterUser_WithMissingFirstName_MayReturnBadRequest()
    {
        // Arrange
        var request = new UserRegisterRequestBuilder()
            .WithFirstName("")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/employees/register", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    #endregion
}

public class UserRegisterResponse
{
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}

public class UserLoginResponse
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public int ExpiresIn { get; set; }
}
