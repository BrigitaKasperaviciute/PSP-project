using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class EmployeeControllerIntegrationTests
{
    [Fact]
    public async Task GetEmployeeById_ValidClaim_ReturnsOkWithEmployee()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(["EmployeesRead"]);

        // Arrange
        var dbEmployeeExists = await factory.ExecuteDbContextAsync(db => db.Users.AnyAsync(x => x.Id == 1));

        // Act
        var response = await client.GetAsync("/api/employees/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("John");
        dbEmployeeExists.Should().BeTrue();
    }

    [Fact]
    public async Task GetEmployeeById_MissingClaim_ReturnsForbidden()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClientWithoutClaims();

        // Arrange
        var dbEmployeeCountBefore = await factory.ExecuteDbContextAsync(db => db.Users.CountAsync());

        // Act
        var response = await client.GetAsync("/api/employees/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var dbEmployeeCountAfter = await factory.ExecuteDbContextAsync(db => db.Users.CountAsync());
        dbEmployeeCountAfter.Should().Be(dbEmployeeCountBefore);
    }
}
