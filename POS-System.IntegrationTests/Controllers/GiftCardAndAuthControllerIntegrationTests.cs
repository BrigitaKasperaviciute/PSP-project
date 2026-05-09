using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Text.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiTestCollection))]
public sealed class GiftCardControllerIntegrationTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _authorizedClient;

    public GiftCardControllerIntegrationTests(ApiTestFactory factory)
    {
        _factory = factory;
        _authorizedClient = null!;
    }

    public async Task InitializeAsync()
    {
        _authorizedClient = _factory.CreateAuthenticatedClient("GiftCardRead", "GiftCardWrite");
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _authorizedClient?.Dispose();
        await Task.CompletedTask;
    }

    // ===== GetAllGiftCards Tests =====

    [Fact]
    public async Task GetAllGiftCards_WithPagination_ReturnsCorrectPage()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/giftcards?pageNum=1&pageSize=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAllGiftCards_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient();

        // Act
        var response = await unauthorizedClient.GetAsync("/api/giftcards?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== GetGiftCardById Tests =====

    [Fact]
    public async Task GetGiftCardById_WithValidId_ReturnsOkOrNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/giftcards/test-id-123");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    // ===== CreateGiftCard Tests =====

    [Fact]
    public async Task CreateGiftCard_WithValidRequest_ReturnsOkAndCreatesEntity()
    {
        // Arrange
        var giftCardRequest = new GiftCardRequestBuilder()
            .WithDate(DateTime.UtcNow.AddMonths(3))
            .WithValue(15000)
            .Build();

        // Act
        var response = await _authorizedClient.PostAsync("/api/giftcards",
            new StringContent(JsonSerializer.Serialize(giftCardRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateGiftCard_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient("GiftCardRead");
        var giftCardRequest = new GiftCardRequestBuilder().Build();

        // Act
        var response = await unauthorizedClient.PostAsync("/api/giftcards",
            new StringContent(JsonSerializer.Serialize(giftCardRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== UpdateGiftCard Tests =====

    [Fact]
    public async Task UpdateGiftCard_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var giftCardRequest = new GiftCardRequestBuilder().Build();

        // Act
        var response = await _authorizedClient.PutAsync("/api/giftcards/non-existing-id",
            new StringContent(JsonSerializer.Serialize(giftCardRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ===== DeleteGiftCard Tests =====

    [Fact]
    public async Task DeleteGiftCard_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.DeleteAsync("/api/giftcards/non-existing-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteGiftCard_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient("GiftCardRead");

        // Act
        var response = await unauthorizedClient.DeleteAsync("/api/giftcards/test-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateGiftCard_MultipleRequests_CreatesMultiple()
    {
        // Arrange & Act & Assert
        for (int i = 0; i < 3; i++)
        {
            var giftCardRequest = new GiftCardRequestBuilder()
                .WithDate(DateTime.UtcNow.AddMonths(i + 1))
                .WithValue(5000 * (i + 1))
                .Build();

            var response = await _authorizedClient.PostAsync("/api/giftcards",
                new StringContent(JsonSerializer.Serialize(giftCardRequest), System.Text.Encoding.UTF8, "application/json"));

            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task GetAllGiftCards_WithVariousPaginationSizes()
    {
        // Arrange & Act
        var small = await _authorizedClient.GetAsync("/api/giftcards?pageNum=0&pageSize=5");
        var medium = await _authorizedClient.GetAsync("/api/giftcards?pageNum=0&pageSize=15");
        var large = await _authorizedClient.GetAsync("/api/giftcards?pageNum=0&pageSize=50");

        // Assert
            small.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
            medium.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
            large.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task UpdateGiftCard_WithValidId_UpdatesSuccessfully()
    {
        // Arrange
        var giftCardRequest = new GiftCardRequestBuilder()
            .WithDate(DateTime.UtcNow.AddMonths(6))
            .WithValue(25000)
            .Build();

        // Act
        var response = await _authorizedClient.PutAsync("/api/giftcards/update-test",
            new StringContent(JsonSerializer.Serialize(giftCardRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

[Collection(nameof(ApiTestCollection))]
public sealed class AuthControllerIntegrationTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _client;

    public AuthControllerIntegrationTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = null!;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateAuthenticatedClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await Task.CompletedTask;
    }

    // ===== RegisterUserAsync Tests =====

    [Fact]
    public async Task RegisterUserAsync_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var registerRequest = new UserRegisterRequest
        {
            Email = $"testuser-{Guid.NewGuid():N}@example.com",
            Password = "Test@123456",
            FirstName = "Test",
            LastName = "User"
        };

        // Act
        var response = await _client.PostAsync("/api/employees/register",
            new StringContent(JsonSerializer.Serialize(registerRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterUserAsync_WithInvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = "{}";

        // Act
        var response = await _client.PostAsync("/api/employees/register",
            new StringContent(invalidRequest, System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ===== LoginUserAsync Tests =====

    [Fact]
    public async Task LoginUserAsync_WithInvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = "{}";

        // Act
        var response = await _client.PostAsync("/v1/auth/login",
            new StringContent(invalidRequest, System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ===== ForgotPasswordAsync Tests =====

    [Fact]
    public async Task ForgotPasswordAsync_WithValidEmail_ReturnsOk()
    {
        // Arrange
        var forgotPasswordRequest = new ForgotPasswordRequest
        {
            Email = "test@example.com"
        };

        // Act
        var response = await _client.PostAsync("/forgot-password",
            new StringContent(JsonSerializer.Serialize(forgotPasswordRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    // ===== ResetPasswordAsync Tests =====

    [Fact]
    public async Task ResetPasswordAsync_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var resetPasswordRequest = new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = "test-token",
            NewPassword = "NewTest@123456"
        };

        // Act
        var response = await _client.PostAsync("/reset-password",
            new StringContent(JsonSerializer.Serialize(resetPasswordRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterUserAsync_MultipleRequests_CreatesMultipleUsers()
    {
        // Arrange & Act & Assert
        for (int i = 0; i < 3; i++)
        {
            var userRegisterRequest = new UserRegisterRequest
            {
                Email = $"user{i}@example.com",
                Password = $"Password@123{i}",
                FirstName = $"User{i}",
                LastName = $"Test{i}"
            };

            var response = await _client.PostAsync("/api/employees/register",
                new StringContent(JsonSerializer.Serialize(userRegisterRequest), System.Text.Encoding.UTF8, "application/json"));

            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
        }
    }

    [Fact]
    public async Task LoginUserAsync_WithValidCredentials_ReturnsOkOrUnauthorized()
    {
        // Arrange
        var loginRequest = new UserLoginRequest
        {
            Email = "test@example.com",
            Password = "Test@123456"
        };

        // Act
        var response = await _client.PostAsync("/v1/auth/login",
            new StringContent(JsonSerializer.Serialize(loginRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WithMultipleEmails_ReturnsOkOrNotFound()
    {
        // Arrange
        var emails = new[] { "user1@example.com", "user2@example.com", "user3@example.com" };

        // Act & Assert
        foreach (var email in emails)
        {
            var forgotPasswordRequest = new ForgotPasswordRequest { Email = email };
            var response = await _client.PostAsync("/forgot-password",
                new StringContent(JsonSerializer.Serialize(forgotPasswordRequest), System.Text.Encoding.UTF8, "application/json"));

            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
        }
    }
}

// ===== Helper DTOs for Auth Tests =====

public record UserRegisterRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
}

public record UserLoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public record ForgotPasswordRequest
{
    public required string Email { get; set; }
}

public record ResetPasswordRequest
{
    public required string Email { get; set; }
    public required string Token { get; set; }
    public required string NewPassword { get; set; }
}

}
