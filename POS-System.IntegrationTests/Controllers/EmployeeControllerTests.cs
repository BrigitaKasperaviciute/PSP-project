using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Data.Identity;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class EmployeeControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public EmployeeControllerTests(PosSystemApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Remove user-role links before removing users/roles
        db.UserRoles.RemoveRange(db.UserRoles.ToList());
        db.RoleClaims.RemoveRange(db.RoleClaims.ToList());
        db.Roles.RemoveRange(db.Roles.ToList());
        db.Users.RemoveRange(db.Users.ToList());
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static ApplicationUser MakeUser(int id, string username, string email) => new()
    {
        Id = id,
        EmployeeId = id,
        FirstName = "First",
        LastName = "Last",
        UserName = username,
        NormalizedUserName = username.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString(),
        ConcurrencyStamp = Guid.NewGuid().ToString(),
        RoleId = 0,
        BirthDate = new DateOnly(1990, 1, 1),
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        IsDeleted = false,
        Version = DateTime.UtcNow
    };

    // ── GET ALL ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllEmployees_WithValidAuth_ReturnsOkWithEmployeeList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Users.Add(MakeUser(1001, "alice", "alice@test.com"));
        db.Users.Add(MakeUser(1002, "bob", "bob@test.com"));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("EmployeesRead");

        // Act
        var response = await client.GetAsync("/api/employees");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<EmployeeResponse>>(_jsonOptions);
        body!.TotalCount.Should().Be(2);
        body.Results.Should().Contain(e => e.UserName == "alice");
    }

    [Fact]
    public async Task GetAllEmployees_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/employees");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET BY ID ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEmployeeById_WithExistingId_ReturnsOkWithEmployee()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Users.Add(MakeUser(1003, "charlie", "charlie@test.com"));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("EmployeesRead");

        // Act
        var response = await client.GetAsync("/api/employees/1003");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>(_jsonOptions);
        body!.UserName.Should().Be("charlie");
        body.Email.Should().Be("charlie@test.com");
    }

    [Fact]
    public async Task GetEmployeeById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("EmployeesRead");

        // Act
        var response = await client.GetAsync("/api/employees/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── UPDATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateEmployee_WithExistingId_ReturnsOkWithUpdatedEmployee()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Create the roles needed for remove/add role operations
        await roleManager.CreateAsync(new ApplicationRole { Name = "None" });
        await roleManager.CreateAsync(new ApplicationRole { Name = "Cashier" });

        db.Users.Add(MakeUser(1004, "dave", "dave@test.com"));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("EmployeesWrite");
        var request = new EmployeeRequest(
            FirstName: "DaveUpdated",
            LastName: "Smith",
            BirthDate: new DateOnly(1985, 3, 15),
            UserName: "dave",
            Email: "dave@test.com",
            PhoneNumber: "123456789",
            RoleId: 2  // Cashier
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/employees/1004", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>(_jsonOptions);
        body!.FirstName.Should().Be("DaveUpdated");
    }

    [Fact]
    public async Task UpdateEmployee_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("EmployeesWrite");
        var request = new EmployeeRequest(
            FirstName: "X",
            LastName: "Y",
            BirthDate: new DateOnly(1990, 1, 1),
            UserName: "xy",
            Email: "xy@test.com",
            PhoneNumber: "1234567890",
            RoleId: 0
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/employees/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteEmployee_WithExistingId_ReturnsOkAndSoftDeletesEmployee()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Users.Add(MakeUser(1005, "eve", "eve@test.com"));
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("EmployeesWrite");

        // Act
        var response = await client.DeleteAsync("/api/employees/1005");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify soft delete in database
        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await assertDb.Users.FindAsync(1005);
        deleted!.IsDeleted.Should().BeTrue();
        deleted.EndDate.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteEmployee_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("EmployeesWrite");

        // Act
        var response = await client.DeleteAsync("/api/employees/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
