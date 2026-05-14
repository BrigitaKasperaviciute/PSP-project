using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using POS_System.IntegrationTests.Infrastructure;
using POS_System.IntegrationTests.Infrastructure.Builders;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class EmployeeControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public EmployeeControllerTests(ApiTestFactory factory)
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
    public async Task GetEmployees_WithValidRequest_ReturnsOkAndSeededEmployees()
    {
        // Arrange
        
        // Act
        var response = await _client.GetAsync("/api/employees?pageNumber=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<POS_System.Business.Dtos.PagedResponse<EmployeeResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetEmployeeById_WithExistingId_ReturnsOkAndEmployee()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/employees/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(1);
        body.UserName.Should().Be("johndoe");
    }

    [Fact]
    public async Task UpdateEmployee_WithValidPayload_ReturnsOkAndUpdatesDatabase()
    {
        // Arrange
        var updateRequest = new EmployeeRequestBuilder()
            .WithFirstName("Updated")
            .WithLastName("Employee")
            .WithUserName("updatedemployee")
            .WithPhoneNumber("9876543210")
            .Build();

        // Act
        var response = await _client.PutAsJsonAsync("/api/employees/1", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body.Should().NotBeNull();
        body!.FirstName.Should().Be("Updated");
        body.LastName.Should().Be("Employee");

        await using var db = _factory.CreateDbContext();
        var persisted = await db.Employees.AsNoTracking().FirstAsync(employee => employee.Id == 1);
        persisted.FirstName.Should().Be("Updated");
        persisted.LastName.Should().Be("Employee");
    }

    [Fact]
    public async Task DeleteEmployee_WithExistingId_ReturnsOkAndMarksEmployeeDeleted()
    {
        // Arrange
        
        // Act
        var response = await _client.DeleteAsync("/api/employees/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = _factory.CreateDbContext();
        var persisted = await db.Employees.AsNoTracking().FirstAsync(employee => employee.Id == 1);
        persisted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateEmployee_WithInvalidPhoneNumber_ReturnsBadRequest()
    {
        // Arrange
        var updateRequest = new EmployeeRequestBuilder()
            .WithPhoneNumber("abc")
            .Build();

        // Act
        var response = await _client.PutAsJsonAsync("/api/employees/1", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetEmployeeById_WithMissingId_ReturnsNotFoundOrError()
    {
        // Act
        var response = await _client.GetAsync("/api/employees/99999");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }
}