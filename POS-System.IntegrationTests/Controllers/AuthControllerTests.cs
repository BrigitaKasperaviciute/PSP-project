using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.Data.Identity;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

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

    // --- ForgotPassword ---

    [Fact]
    public async Task ForgotPassword_WithRegisteredEmail_ReturnsOkWithMessage()
    {
        // Arrange
        var registerRequest = BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/employees/register", registerRequest);
        var forgotRequest = new ForgotPasswordRequest { Email = registerRequest.Email };

        // Act
        var response = await _client.PostAsJsonAsync("/forgot-password", forgotRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PasswordRecoveryResponse>();
        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ForgotPassword_WithNonExistentEmail_StillReturnsOkWithoutLeakingInfo()
    {
        // Arrange – by design, the endpoint always returns 200 regardless of whether the email exists
        var forgotRequest = new ForgotPasswordRequest { Email = "no-such-user@nowhere.com" };

        // Act
        var response = await _client.PostAsJsonAsync("/forgot-password", forgotRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PasswordRecoveryResponse>();
        body!.Message.Should().NotBeNullOrEmpty();
    }

    // --- ResetPassword ---

    [Fact]
    public async Task ResetPassword_WithValidToken_ReturnsOkAndPasswordIsChanged()
    {
        // Arrange – register a user, then obtain a valid reset token via UserManager directly
        // (FakeEmailSender discards the email, so the token must be generated out-of-band)
        var registerRequest = BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/employees/register", registerRequest);

        string resetToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(registerRequest.Email);
            resetToken = await userManager.GeneratePasswordResetTokenAsync(user!);
        }

        var resetRequest = new ResetPasswordRequest
        {
            Email = registerRequest.Email,
            ResetCode = resetToken,
            NewPassword = "NewStrongPass@9999!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/reset-password", resetRequest);

        // Assert – HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PasswordRecoveryResponse>();
        body!.Message.Should().NotBeNullOrEmpty();

        // Assert – new password works for login
        var loginResponse = await _client.PostAsJsonAsync("/v1/auth/login",
            new UserLoginRequest(UserName: registerRequest.UserName, Password: "NewStrongPass@9999!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_Returns400()
    {
        // Arrange – register a user then try to reset with a garbage token
        var registerRequest = BuildRegisterRequest();
        await _client.PostAsJsonAsync("/api/employees/register", registerRequest);

        var resetRequest = new ResetPasswordRequest
        {
            Email = registerRequest.Email,
            ResetCode = "completely-invalid-token",
            NewPassword = "NewPass@1234!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/reset-password", resetRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error.Should().NotBeNull();
        error!.Status.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_WithNonExistentEmail_Returns400()
    {
        // Arrange
        var resetRequest = new ResetPasswordRequest
        {
            Email = "ghost@nowhere.com",
            ResetCode = "any-token",
            NewPassword = "NewPass@1234!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/reset-password", resetRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.BadRequest);
    }
}
