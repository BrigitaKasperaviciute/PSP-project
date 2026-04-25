using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Integration.Tests.Infrastructure;

namespace POS_System.Integration.Tests.Controllers;

public class EmployeeControllerTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public EmployeeControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _authClient = factory.CreateClient();
        TestAuthHelper.AddFullAccessAuth(_authClient);
        _anonClient = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.EnsureInitializedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Seeded employees: Id=1 (johndoe), Id=2 (janedoe), Id=3 (adamsmith), Id=4 (bobjohnson), Id=5 (johnsondoe)

    [Fact]
    public async Task GetAll_ValidToken_ReturnsOkWithPagedResult()
    {
        // Arrange
        // (seeded employees)

        // Act
        var response = await _authClient.GetAsync("/api/employees");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<EmployeeResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
        body.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAll_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no token)

        // Act
        var response = await _anonClient.GetAsync("/api/employees");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithEmployee()
    {
        // Arrange
        const int existingId = 1; // seeded "johndoe"

        // Act
        var response = await _authClient.GetAsync($"/api/employees/{existingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(existingId);
        body.UserName.Should().Be("johndoe");
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _authClient.GetAsync($"/api/employees/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ExistingId_ReturnsOkWithUpdatedEmployee()
    {
        // Arrange – register a fresh employee so the test owns its data
        var registered = await RegisterFreshEmployeeAsync("emp_update_test");
        var updateRequest = new EmployeeRequest(
            FirstName: "UpdatedFirst",
            LastName: "UpdatedLast",
            BirthDate: new DateOnly(1990, 5, 15),
            UserName: "emp_update_test",
            Email: "emp_update_test@test.com",
            PhoneNumber: "111222333",
            RoleId: 1);

        // Act
        var response = await _authClient.PutAsJsonAsync($"/api/employees/{registered.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.Should().NotBeNull();
        body!.FirstName.Should().Be("UpdatedFirst");
        body.LastName.Should().Be("UpdatedLast");

        // Verify persistence
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Employees.FindAsync(registered.Id);
        saved!.FirstName.Should().Be("UpdatedFirst");
    }

    [Fact]
    public async Task Update_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new EmployeeRequest("F", "L", new DateOnly(2000, 1, 1), "x", "x@x.com", "123456789", 1);

        // Act
        var response = await _authClient.PutAsJsonAsync("/api/employees/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingId_ReturnsOkAndSoftDeletesEmployee()
    {
        // Arrange – create a dedicated employee to delete
        var registered = await RegisterFreshEmployeeAsync("emp_delete_test");

        // Act
        var response = await _authClient.DeleteAsync($"/api/employees/{registered.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.Should().NotBeNull();
        body!.IsDeleted.Should().BeTrue();

        // Verify soft-delete in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await db.Employees.FindAsync(registered.Id);
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        // (nothing to set up)

        // Act
        var response = await _authClient.DeleteAsync("/api/employees/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Helper: register a new employee via the Auth endpoint and return the response.
    private async Task<EmployeeResponse> RegisterFreshEmployeeAsync(string userName)
    {
        var request = new UserRegisterRequest(
            Email: $"{userName}@test.com",
            UserName: userName,
            FirstName: "Test",
            LastName: "User",
            Password: "Test@1234!",
            PhoneNumber: "123456789",
            BirthDate: new DateOnly(1995, 6, 15),
            RoleId: 1 // "Service provider"
        );
        var response = await _authClient.PostAsJsonAsync("/api/employees/register", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EmployeeResponse>())!;
    }
}
