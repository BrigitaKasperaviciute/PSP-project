using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Data.Identity;
using POS_System.IntegrationTests;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class AuthControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AuthControllerTests(PosSystemApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAnonymousClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Register ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidData_ReturnsOkWithEmployeeResponse()
    {
        // Arrange
        var request = new UserRegisterRequest(
            Email: "newemployee@test.com",
            UserName: "newemployee",
            FirstName: "Alice",
            LastName: "Smith",
            Password: "Test@1234!",
            PhoneNumber: "37060000001",
            BirthDate: new DateOnly(1990, 6, 15),
            RoleId: 2 // Cashier
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/employees/register", request);
        var body = await response.Content.ReadAsStringAsync();
        var employee = JsonSerializer.Deserialize<EmployeeResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        employee.Should().NotBeNull();
        employee!.UserName.Should().Be("newemployee");
        employee.FirstName.Should().Be("Alice");

        // Verify database state
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var createdUser = await userManager.FindByNameAsync("newemployee");
        createdUser.Should().NotBeNull();
        createdUser!.Email.Should().Be("newemployee@test.com");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange — register a user first
        var first = new UserRegisterRequest(
            Email: "duplicate@test.com",
            UserName: "user_a",
            FirstName: "A",
            LastName: "B",
            Password: "Test@1234!",
            PhoneNumber: "37060000002",
            BirthDate: new DateOnly(1992, 1, 1),
            RoleId: 2
        );
        await _client.PostAsJsonAsync("/api/employees/register", first);

        var duplicate = new UserRegisterRequest(
            Email: "duplicate@test.com",  // same email
            UserName: "user_b",
            FirstName: "C",
            LastName: "D",
            Password: "Test@1234!",
            PhoneNumber: "37060000003",
            BirthDate: new DateOnly(1993, 1, 1),
            RoleId: 2
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/employees/register", duplicate);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Login ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithJwtToken()
    {
        // Arrange — register then immediately login
        var registerRequest = new UserRegisterRequest(
            Email: "loginuser@test.com",
            UserName: "loginuser",
            FirstName: "Login",
            LastName: "User",
            Password: "Login@5678!",
            PhoneNumber: "37060000004",
            BirthDate: new DateOnly(1988, 3, 20),
            RoleId: 2
        );
        await _client.PostAsJsonAsync("/api/employees/register", registerRequest);

        var loginRequest = new UserLoginRequest("loginuser", "Login@5678!");

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/login", loginRequest);
        var body = await response.Content.ReadAsStringAsync();
        var loginResponse = JsonSerializer.Deserialize<UserLoginResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        loginResponse.Should().NotBeNull();
        loginResponse!.UserName.Should().Be("loginuser");
        loginResponse.JwtToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange — register user then try wrong password
        var registerRequest = new UserRegisterRequest(
            Email: "wrongpass@test.com",
            UserName: "wrongpassuser",
            FirstName: "Wrong",
            LastName: "Pass",
            Password: "Correct@1234!",
            PhoneNumber: "37060000005",
            BirthDate: new DateOnly(1995, 7, 7),
            RoleId: 2
        );
        await _client.PostAsJsonAsync("/api/employees/register", registerRequest);

        var loginRequest = new UserLoginRequest("wrongpassuser", "Wrong@Password!");

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentUsername_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new UserLoginRequest("nobody", "Password@1!");

        // Act
        var response = await _client.PostAsJsonAsync("/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
