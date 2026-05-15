using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class EmployeeControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public EmployeeControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClientWithClaims("EmployeesRead", "EmployeesWrite");
    }

    // Employees are not cleaned up – seeded employees (IDs 1–5) are used for reads,
    // and new employees created in write tests use unique emails so they don't conflict.
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    private static UserRegisterRequest BuildRegisterRequest()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        return new UserRegisterRequest(
            Email: $"emp-{unique}@test.com",
            UserName: $"emp-{unique}",
            FirstName: "Test",
            LastName: "Employee",
            Password: "Test@1234!",
            PhoneNumber: "37060000002",
            BirthDate: new DateOnly(1990, 1, 1),
            RoleId: 2
        );
    }

    // --- GetAll ---

    [Fact]
    public async Task GetAll_WithEmployeesReadClaim_ReturnsOkAndNotEmpty()
    {
        var response = await _client.GetAsync("/api/employees");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedEmployeeResponse<EmployeeResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/employees");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsEmployee()
    {
        // Seeded employee ID 1 is always present
        var response = await _client.GetAsync("/api/employees/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body!.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        var response = await _client.GetAsync("/api/employees/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidPayload_ReturnsUpdatedEmployee()
    {
        // Arrange – register a fresh employee to avoid mutating seeded data
        var registerRequest = BuildRegisterRequest();
        var registered = await (await _client.PostAsJsonAsync("/api/employees/register", registerRequest))
            .Content.ReadFromJsonAsync<EmployeeResponse>();

        var unique = Guid.NewGuid().ToString("N")[..8];
        var updateRequest = new EmployeeRequest(
            FirstName: "Updated",
            LastName: "Person",
            BirthDate: new DateOnly(1995, 6, 15),
            UserName: $"upd-{unique}",
            Email: $"upd-{unique}@test.com",
            PhoneNumber: "37062222222",
            RoleId: 2
        );

        // Act
        var response = await _client.PutAsJsonAsync($"/api/employees/{registered!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body!.FirstName.Should().Be("Updated");
    }

    [Fact]
    public async Task Update_WithoutWriteClaim_Returns403()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var readOnlyClient = _factory.CreateClientWithClaims("EmployeesRead");
        var response = await readOnlyClient.PutAsJsonAsync("/api/employees/1",
            new EmployeeRequest("A", "B", new DateOnly(1990, 1, 1), $"u-{unique}", $"u-{unique}@t.com", "37060000000", 2));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOk()
    {
        // Arrange – create a new employee so we don't delete seeded ones
        var registerRequest = BuildRegisterRequest();
        var registered = await (await _client.PostAsJsonAsync("/api/employees/register", registerRequest))
            .Content.ReadFromJsonAsync<EmployeeResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/employees/{registered!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        body!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/employees/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

file record PagedEmployeeResponse<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
