using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class EmployeeControllerTests
{
    [Fact]
    public async Task UpdateAndDeleteEmployee_ReturnsExpectedState()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "EmployeesRead,EmployeesWrite");

        var listResponse = await client.GetAsync("api/employees?onlyActive=true&pageNumber=0&pageSize=10");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, listBody);
        listBody.Should().Contain("John");

        var getResponse = await client.GetAsync("api/employees/1");
        var getBody = await getResponse.Content.ReadAsStringAsync();
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, getBody);
        getBody.Should().Contain("johndoe");

        var updateRequest = new EmployeeRequest(
            FirstName: "Updated",
            LastName: "Employee",
            BirthDate: new DateOnly(1990, 1, 1),
            UserName: "updatedemployee",
            Email: "updatedemployee@example.com",
            PhoneNumber: "777777777",
            RoleId: 4
        );

        var updateResponse = await client.PutAsync("api/employees/1", TestDataFactory.ToJsonContent(updateRequest));
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, updateBody);
        updateBody.Should().Contain(updateRequest.FirstName);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        var updatedEmployee = db.Employees.Single(employee => employee.Id == 1);
        updatedEmployee.FirstName.Should().Be(updateRequest.FirstName);
        updatedEmployee.RoleId.Should().Be(4);

        var deleteResponse = await client.DeleteAsync("api/employees/2");
        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK, deleteBody);
        db.Employees.Single(employee => employee.Id == 2).IsDeleted.Should().BeTrue();
        db.Employees.Single(employee => employee.Id == 2).EndDate.Should().NotBeNull();
    }

    [Fact]
    public async Task GetEmployeeById_NonexistentId_ReturnsNotFound()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "EmployeesRead");

        var response = await client.GetAsync("api/employees/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}