using Microsoft.AspNetCore.Identity.Data;
using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using POS_System.IntegrationTests.Infrastructure;

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
        _client = factory.CreateAuthenticatedClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegisterUser_WithValidPayload_ReturnsOkAndPersistsEmployee()
    {
        // Arrange
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var request = new UserRegisterRequest(
            email,
            $"user-{Guid.NewGuid():N}",
            "Alice",
            "Johnson",
            "Passw0rd!",
            "1234567890",
            new DateOnly(1995, 1, 1),
            4);

        // Act
        var response = await _client.PostAsJsonAsync("/api/employees/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.Should().NotBeNull();
        body!.Email.Should().Be(email);
        body.UserName.Should().StartWith("user-");
    }

    [Fact]
    public async Task RegisterUser_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var email = $"duplicate-{Guid.NewGuid():N}@example.com";
        var request = new UserRegisterRequest(
            email,
            $"user-{Guid.NewGuid():N}",
            "Alice",
            "Johnson",
            "Passw0rd!",
            "1234567890",
            new DateOnly(1995, 1, 1),
            4);

        await _client.PostAsJsonAsync("/api/employees/register", request);

        // Act
        var response = await _client.PostAsJsonAsync("/api/employees/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LoginUser_WithValidCredentials_ReturnsOkAndJwtToken()
    {
        // Arrange
        var email = $"login-{Guid.NewGuid():N}@example.com";
        var userName = $"login-{Guid.NewGuid():N}";
        var password = "Passw0rd!";

        var registerRequest = new UserRegisterRequest(
            email,
            userName,
            "Alice",
            "Johnson",
            password,
            "1234567890",
            new DateOnly(1995, 1, 1),
            4);

        await _client.PostAsJsonAsync("/api/employees/register", registerRequest);

        var loginRequest = new UserLoginRequest(userName, password);

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        body.Should().NotBeNull();
        body!.UserName.Should().Be(userName);
        body.JwtToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LoginUser_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var email = $"login-fail-{Guid.NewGuid():N}@example.com";
        var userName = $"loginfail-{Guid.NewGuid():N}";
        var password = "Passw0rd!";

        var registerRequest = new UserRegisterRequest(
            email,
            userName,
            "Alice",
            "Johnson",
            password,
            "1234567890",
            new DateOnly(1995, 1, 1),
            4);

        await _client.PostAsJsonAsync("/api/employees/register", registerRequest);

        var loginRequest = new UserLoginRequest(userName, "WrongPass1!");

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForgotPassword_WithMissingUser_ReturnsOk()
    {
        // Arrange
        var request = new ForgotPasswordRequest { Email = $"missing-{Guid.NewGuid():N}@example.com" };

        // Act
        var response = await _client.PostAsJsonAsync("/forgot-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PasswordRecoveryResponse>();
        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ResetPassword_WithMissingUser_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = $"missing-{Guid.NewGuid():N}@example.com",
            ResetCode = "invalid-code",
            NewPassword = "Passw0rd!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/reset-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}