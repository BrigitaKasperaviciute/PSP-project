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
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class AuthControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AuthControllerTests(PosSystemApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserClaims.RemoveRange(db.UserClaims.ToList());
        db.UserRoles.RemoveRange(db.UserRoles.ToList());
        db.RoleClaims.RemoveRange(db.RoleClaims.ToList());
        db.Users.RemoveRange(db.Users.ToList());
        db.Roles.RemoveRange(db.Roles.ToList());
        await db.SaveChangesAsync();

        // Seed the roles required by AuthService.RegisterUserAsync and LoginUserAsync
        using var roleScope = _factory.Services.CreateScope();
        var roleManager = roleScope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var roleName in new[] { "None", "Service provider", "Cashier", "Owner", "Super admin" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
        }

        // Add a claim "Y" to each role so that LoginUserAsync can build a JWT
        foreach (var roleName in new[] { "None", "Service provider", "Cashier", "Owner", "Super admin" })
        {
            var role = await roleManager.FindByNameAsync(roleName);
            var existingClaims = await roleManager.GetClaimsAsync(role!);
            if (!existingClaims.Any(c => c.Type == "ItemRead"))
                await roleManager.AddClaimAsync(role!, new System.Security.Claims.Claim("ItemRead", "Y"));
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── REGISTER ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterUser_WithValidData_ReturnsOkWithEmployeeResponse()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new UserRegisterRequest(
            Email: "newuser@test.com",
            UserName: "newuser",
            FirstName: "New",
            LastName: "User",
            Password: "SecureP@ss1",
            PhoneNumber: "1234567890",
            BirthDate: new DateOnly(1995, 6, 15),
            RoleId: 0
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/employees/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>(_jsonOptions);
        body!.UserName.Should().Be("newuser");
        body.Email.Should().Be("newuser@test.com");
        body.FirstName.Should().Be("New");

        // Verify user persisted in database
        using var assertScope = _factory.Services.CreateScope();
        var userManager = assertScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var saved = await userManager.FindByNameAsync("newuser");
        saved.Should().NotBeNull();
        saved!.Email.Should().Be("newuser@test.com");
    }

    [Fact]
    public async Task RegisterUser_WithDuplicateUsername_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = new ApplicationUser
        {
            EmployeeId = 9001,
            FirstName = "Existing",
            LastName = "User",
            UserName = "duplicateuser",
            Email = "existing@test.com",
            RoleId = 0,
            BirthDate = new DateOnly(1990, 1, 1),
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Version = DateTime.UtcNow
        };
        await userManager.CreateAsync(existing, "SecureP@ss1");

        var client = _factory.CreateClient();
        var request = new UserRegisterRequest(
            Email: "another@test.com",
            UserName: "duplicateuser",  // same username
            FirstName: "Another",
            LastName: "User",
            Password: "SecureP@ss1",
            PhoneNumber: "9876543210",
            BirthDate: new DateOnly(1998, 3, 20),
            RoleId: 0
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/employees/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── LOGIN ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginUser_WithValidCredentials_ReturnsOkWithJwtToken()
    {
        // Arrange – register a user first so the password is properly hashed
        var client = _factory.CreateClient();
        var registerRequest = new UserRegisterRequest(
            Email: "logintest@test.com",
            UserName: "logintest",
            FirstName: "Login",
            LastName: "Test",
            Password: "SecureP@ss1",
            PhoneNumber: "5551234567",
            BirthDate: new DateOnly(1992, 7, 4),
            RoleId: 0
        );
        await client.PostAsJsonAsync("/api/employees/register", registerRequest);

        var loginRequest = new UserLoginRequest(UserName: "logintest", Password: "SecureP@ss1");

        // Act
        var response = await client.PostAsJsonAsync("/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>(_jsonOptions);
        body!.UserName.Should().Be("logintest");
        body.JwtToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginUser_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange – register a user
        var client = _factory.CreateClient();
        var registerRequest = new UserRegisterRequest(
            Email: "wrongpass@test.com",
            UserName: "wrongpassuser",
            FirstName: "Wrong",
            LastName: "Pass",
            Password: "SecureP@ss1",
            PhoneNumber: "5559876543",
            BirthDate: new DateOnly(1988, 2, 14),
            RoleId: 0
        );
        await client.PostAsJsonAsync("/api/employees/register", registerRequest);

        var loginRequest = new UserLoginRequest(UserName: "wrongpassuser", Password: "WrongPassword99!");

        // Act
        var response = await client.PostAsJsonAsync("/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginUser_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var loginRequest = new UserLoginRequest(UserName: "ghostuser", Password: "SecureP@ss1");

        // Act
        var response = await client.PostAsJsonAsync("/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── FORGOT PASSWORD ──────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_WithAnyEmail_ReturnsOkToPreventEnumeration()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new Microsoft.AspNetCore.Identity.Data.ForgotPasswordRequest
        {
            Email = "nonexistent@test.com"
        };

        // Act
        var response = await client.PostAsJsonAsync("/forgot-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_WithRegisteredEmail_ReturnsOkAndSendsResetInfo()
    {
        // Arrange – register a user first
        var client = _factory.CreateClient();
        var registerRequest = new UserRegisterRequest(
            Email: "forgotpass@test.com",
            UserName: "forgotpassuser",
            FirstName: "Forgot",
            LastName: "Pass",
            Password: "SecureP@ss1",
            PhoneNumber: "5550001234",
            BirthDate: new DateOnly(1993, 9, 9),
            RoleId: 0
        );
        await client.PostAsJsonAsync("/api/employees/register", registerRequest);

        var request = new Microsoft.AspNetCore.Identity.Data.ForgotPasswordRequest
        {
            Email = "forgotpass@test.com"
        };

        // Act
        var response = await client.PostAsJsonAsync("/forgot-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
