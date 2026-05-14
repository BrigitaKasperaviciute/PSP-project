using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Identity;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection("Integration tests")]
public sealed class AuthControllerTests : IntegrationTestBase
{
    public AuthControllerTests(ApiTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task RegisterUserAsync_creates_user_and_rejects_invalid_birthdate()
    {
        await ResetDatabaseAsync();

        var client = CreateClient();
        var registerRequest = new UserRegisterRequest(
            "auth.valid@example.com",
            "authvalid",
            "Auth",
            "User",
            "Password1!",
            "12345678901",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
            0);

        var successResponse = await client.PostAsJsonAsync("/api/employees/register", registerRequest);
        successResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await WithDbContextAsync(async context =>
        {
            var user = await context.Users.SingleAsync(user => user.Email == registerRequest.Email);
            user.UserName.Should().Be(registerRequest.UserName);
        });

        var invalidRequest = new UserRegisterRequest(
            "auth.invalid@example.com",
            "authinvalidyoung",
            "Auth",
            "User",
            "Password1!",
            "12345678901",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-10)),
            0);

        var badResponse = await client.PostAsJsonAsync("/api/employees/register", invalidRequest);
        badResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LoginUserAsync_returns_token_and_rejects_wrong_password()
    {
        await ResetDatabaseAsync();

        var client = CreateClient();
        var registerRequest = new UserRegisterRequest(
            "auth.login@example.com",
            "authlogin",
            "Auth",
            "Login",
            "Password1!",
            "12345678901",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-22)),
            0);

        (await client.PostAsJsonAsync("/api/employees/register", registerRequest)).EnsureSuccessStatusCode();

        var loginResponse = await client.PostAsJsonAsync("/v1/auth/login", new UserLoginRequest(registerRequest.UserName, registerRequest.Password));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<UserLoginResponse>();
        loginPayload.Should().NotBeNull();
        loginPayload!.UserName.Should().Be(registerRequest.UserName);
        loginPayload.JwtToken.Should().NotBeNullOrWhiteSpace();

        var unauthorizedResponse = await client.PostAsJsonAsync("/v1/auth/login", new UserLoginRequest(registerRequest.UserName, "WrongPassword1!"));
        unauthorizedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PasswordRecoveryScenario_covers_forgot_and_reset_password_routes()
    {
        await ResetDatabaseAsync();

        var client = CreateClient();
        var registerRequest = new UserRegisterRequest(
            "auth.recovery@example.com",
            "authrecovery",
            "Auth",
            "Recovery",
            "Password1!",
            "12345678901",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-22)),
            0);

        (await client.PostAsJsonAsync("/api/employees/register", registerRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PostAsJsonAsync("/forgot-password", new ForgotPasswordRequest { Email = registerRequest.Email })).StatusCode.Should().Be(HttpStatusCode.OK);

        string resetToken;
        using (var scope = Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(registerRequest.Email);
            user.Should().NotBeNull();
            resetToken = await userManager.GeneratePasswordResetTokenAsync(user!);
        }

        var resetRequest = new ResetPasswordRequest
        {
            Email = registerRequest.Email,
            ResetCode = resetToken,
            NewPassword = "Password2!"
        };
        (await client.PostAsJsonAsync("/reset-password", resetRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        var invalidResetRequest = new ResetPasswordRequest
        {
            Email = "missing@example.com",
            ResetCode = "invalid-token",
            NewPassword = "Password3!"
        };
        (await client.PostAsJsonAsync("/reset-password", invalidResetRequest)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
