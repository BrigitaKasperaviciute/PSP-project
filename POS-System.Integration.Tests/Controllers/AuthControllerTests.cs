using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace POS_System.Integration.Tests.Controllers;

public class AuthControllerTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    // Credentials for the pre-created login test user
    private const string LoginUserName = "auth_login_user";
    private const string LoginPassword = "Test@1234!";

    public AuthControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.EnsureInitializedAsync();
        // Pre-create a user so that login tests have valid credentials to use.
        await _factory.CreateTestUserAsync(LoginUserName, LoginPassword, "Super admin");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_ValidRequest_ReturnsOkWithEmployeeResponse()
    {
        // Arrange
        var request = new UserRegisterRequest(
            Email: "newreg_user@test.com",
            UserName: "newreg_user",
            FirstName: "New",
            LastName: "User",
            Password: "Test@1234!",
            PhoneNumber: "987654321",
            BirthDate: new DateOnly(1995, 3, 20),
            RoleId: 1 // "Service provider" – seeded in factory
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/employees/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.Should().NotBeNull();
        body!.UserName.Should().Be("newreg_user");
        body.FirstName.Should().Be("New");
        body.IsDeleted.Should().BeFalse();

        // Verify user was persisted in Identity store
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Employees.FirstOrDefaultAsync(e => e.UserName == "newreg_user");
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_DuplicateUserName_ReturnsBadRequest()
    {
        // Arrange – first registration creates the user
        var request = new UserRegisterRequest(
            Email: "dup_user@test.com",
            UserName: "dup_user",
            FirstName: "Dup",
            LastName: "User",
            Password: "Test@1234!",
            PhoneNumber: "111000111",
            BirthDate: new DateOnly(1990, 1, 1),
            RoleId: 1
        );
        await _client.PostAsJsonAsync("/api/employees/register", request);

        // Second registration with the same username
        var duplicateRequest = request with { Email = "dup_user2@test.com" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/employees/register", duplicateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithJwtToken()
    {
        // Arrange – user was created in InitializeAsync
        var request = new UserLoginRequest(LoginUserName, LoginPassword);

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        body.Should().NotBeNull();
        body!.UserName.Should().Be(LoginUserName);
        body.JwtToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var request = new UserLoginRequest(LoginUserName, "WrongPassword!99");

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_NonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var request = new UserLoginRequest("ghost_user_xyz", "Test@1234!");

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForgotPassword_ValidEmail_ReturnsOkWithRecoveryResponse()
    {
        // Arrange – uses the pre-created login user's email
        var request = new Microsoft.AspNetCore.Identity.Data.ForgotPasswordRequest
        {
            Email = $"{LoginUserName}@test.com"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/forgot-password", request);

        // Assert – service always responds OK regardless of whether the user exists
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PasswordRecoveryResponse>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_StillReturnsOk()
    {
        // Arrange – per security best-practice the endpoint always returns OK
        var request = new Microsoft.AspNetCore.Identity.Data.ForgotPasswordRequest
        {
            Email = "nobody@nowhere.invalid"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/forgot-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
