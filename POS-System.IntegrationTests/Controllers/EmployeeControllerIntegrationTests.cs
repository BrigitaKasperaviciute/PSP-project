using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class EmployeeControllerIntegrationTests
{
    [Fact]
    public async Task GetEmployeeById_SeededEmployeeReturnsEmployee()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "EmployeesRead" });

        // Act
        var response = await client.GetAsync("/api/employees/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var employee = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        employee.Should().NotBeNull();
        employee!.Id.Should().Be(1);
        employee.UserName.Should().Be("johndoe");
    }

    [Fact]
    public async Task GetEmployees_SeededDataReturnsActiveEmployees()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "EmployeesRead" });

        // Arrange

        // Act
        var response = await client.GetAsync("/api/employees?onlyActive=true&pageNumber=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var employees = await response.Content.ReadFromJsonAsync<PagedResponse<EmployeeResponse>>();
        employees.Should().NotBeNull();
        employees!.Results.Should().NotBeEmpty();
        employees.Results.Should().Contain(employee => employee.UserName == "johndoe");
    }

    [Fact]
    public async Task UpdateEmployeeById_ExistingEmployeePersistsChangesAndReturnsResponse()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "EmployeesWrite", "EmployeesRead" });

        // Arrange
        var request = new EmployeeRequest(
            FirstName: "Updated",
            LastName: "Employee",
            BirthDate: new DateOnly(1990, 1, 1),
            UserName: "johndoe",
            Email: "johndoe@example.com",
            PhoneNumber: "9999999999",
            RoleId: 1
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/employees/1", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedEmployee = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        updatedEmployee.Should().NotBeNull();
        updatedEmployee!.FirstName.Should().Be(request.FirstName);
        updatedEmployee.LastName.Should().Be(request.LastName);
        updatedEmployee.PhoneNumber.Should().Be(request.PhoneNumber);

        var persistedEmployee = await factory.ExecuteDbContextAsync(db => db.Users.SingleAsync(x => x.Id == 1));
        persistedEmployee.FirstName.Should().Be(request.FirstName);
        persistedEmployee.PhoneNumber.Should().Be(request.PhoneNumber);
    }

    [Fact]
    public async Task DeleteEmployeeById_ExistingEmployeeSoftDeletesEmployeeAndReturnsResponse()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "EmployeesWrite" });

        // Arrange

        // Act
        var response = await client.DeleteAsync("/api/employees/2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var deletedEmployee = await factory.ExecuteDbContextAsync(db => db.Users.SingleAsync(x => x.Id == 2));
        deletedEmployee.IsDeleted.Should().BeTrue();
        deletedEmployee.EndDate.Should().NotBeNull();
    }
}