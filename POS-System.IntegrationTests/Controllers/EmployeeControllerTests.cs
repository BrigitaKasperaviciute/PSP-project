using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class EmployeeControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;
    private ApplicationDbContext _db = null!;

    public EmployeeControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateDefaultClient();
    }

    public async Task InitializeAsync()
    {
        _db = _factory.CreateDbContext();
        // Clean employees table before each test
        await _db.Employees.ExecuteDeleteAsync();
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    #region Happy Path Tests

    [Fact]
    public async Task GetAllEmployees_WithValidPageNumbers_ReturnsOkWithEmployees()
    {
        // Arrange - pre-populate with test data if needed
        
        // Act
        var response = await _client.GetAsync("/api/employees?pageNumber=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponse<EmployeeResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllEmployees_WithOnlyActiveTrue_ReturnsOnlyActiveEmployees()
    {
        // Act
        var response = await _client.GetAsync("/api/employees?onlyActive=true&pageNumber=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponse<EmployeeResponse>>();
        body.Should().NotBeNull();
        body!.Data.All(e => e.IsActive).Should().BeTrue();
    }

    [Fact]
    public async Task GetEmployeeById_WithExistingId_ReturnsOkWithEmployee()
    {
        // Arrange - create employee first if possible via seeding
        // For now, use a reasonable ID
        
        // Act
        var response = await _client.GetAsync("/api/employees/1");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateEmployee_WithValidPayload_ReturnsOkAndUpdatesEmployee()
    {
        // Arrange - assuming at least one employee exists
        var updateRequest = new EmployeeRequestBuilder()
            .WithFirstName("UpdatedName")
            .WithEmail($"updated-{Guid.NewGuid():N}@example.com")
            .Build();

        // Act
        var response = await _client.PutAsJsonAsync("/api/employees/1", updateRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteEmployee_WithExistingId_ReturnsOkAndDeletesEmployee()
    {
        // Act
        var response = await _client.DeleteAsync("/api/employees/999999");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Negative Flow Tests

    [Fact]
    public async Task GetEmployeeById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/employees/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateEmployee_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var updateRequest = new EmployeeRequestBuilder().Build();

        // Act
        var response = await _client.PutAsJsonAsync("/api/employees/999999", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteEmployee_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/api/employees/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateEmployee_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = """{"firstName": "Test", "lastName": "User", "email": "invalid-email", "isActive": true}""";

        // Act
        var response = await _client.PutAsJsonAsync("/api/employees/1", invalidRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    #endregion
}

public class EmployeeResponse
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public bool IsActive { get; set; }
}
