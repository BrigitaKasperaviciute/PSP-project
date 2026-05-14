using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Data.Identity;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection("Integration tests")]
public sealed class EmployeeControllerTests : IntegrationTestBase
{
    public EmployeeControllerTests(ApiTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task EmployeeScenario_covers_happy_path_routes()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        (await client.GetAsync("/api/employees?onlyActive=true&pageNumber=0&pageSize=10")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/employees/1")).StatusCode.Should().Be(HttpStatusCode.OK);

        var updateRequest = new EmployeeRequest(
            "EmployeeUpdated",
            "Updated",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
            "employeeupdated",
            "employee.updated@example.com",
            "12345678901",
            0);

        (await client.PutAsJsonAsync("/api/employees/4", updateRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync("/api/employees/4")).StatusCode.Should().Be(HttpStatusCode.OK);

        await WithDbContextAsync(async context =>
        {
            var deletedEmployee = await context.Users.SingleAsync(user => user.Id == 4);
            deletedEmployee.IsDeleted.Should().BeTrue();
            deletedEmployee.EndDate.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task EmployeeEndpoints_return_validation_and_not_found_errors()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        var invalidRequest = new EmployeeRequest(
            "Employee",
            "Invalid",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-10)),
            "",
            "employee.invalid@example.com",
            "abc",
            0);

        (await client.PutAsJsonAsync("/api/employees/1", invalidRequest)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/employees/99999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
