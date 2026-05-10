using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Fixtures;
using POS_System.IntegrationTests.Helpers;

namespace POS_System.IntegrationTests.Tests;

[Collection("Integration")]
public class EmployeeControllerTests(PosWebApplicationFactory factory)
{
    private readonly HttpClient _client = CreateAuthClient(factory);
    private readonly HttpClient _anonClient = factory.CreateClient();

    private static HttpClient CreateAuthClient(PosWebApplicationFactory f)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.FullAccessToken);
        return c;
    }

    // GET /api/employees/{id}
    [Fact]
    public async Task GetEmployeeById_ExistingId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/employees/2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEmployeeById_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/employees/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // PUT /api/employees/{employeeId}
    [Fact]
    public async Task UpdateEmployee_ExistingId_ReturnsOk()
    {
        var updateReq = new EmployeeRequest(
            FirstName: "UpdatedFirst",
            LastName: "UpdatedLast",
            BirthDate: new DateOnly(1990, 5, 15),
            UserName: "janedoe",
            Email: "janedoe@example.com",
            PhoneNumber: "77567455",
            RoleId: 0);

        var response = await _client.PutAsJsonAsync("/api/employees/2", updateReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEmployee_NonExistentId_ReturnsNotFound()
    {
        var updateReq = new EmployeeRequest(
            FirstName: "X",
            LastName: "Y",
            BirthDate: new DateOnly(1990, 1, 1),
            UserName: "xyz",
            Email: "xyz@example.com",
            PhoneNumber: "1234567890",
            RoleId: 0);

        var response = await _client.PutAsJsonAsync("/api/employees/99999", updateReq);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DELETE /api/employees/{employeeId}
    [Fact]
    public async Task DeleteEmployee_AfterRegister_ReturnsOk()
    {
        var uniqueUser = $"del_{Guid.NewGuid():N}";
        var registerReq = new UserRegisterRequest(
            Email: $"{uniqueUser}@example.com",
            UserName: uniqueUser,
            FirstName: "Del",
            LastName: "User",
            Password: "Test@1234",
            PhoneNumber: "9876543210",
            BirthDate: new DateOnly(1995, 6, 15),
            RoleId: 0);

        var registered = await _anonClient.PostAsJsonAsync("/api/employees/register", registerReq);
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        var employee = await registered.Content.ReadFromJsonAsync<EmployeeIdResponse>();

        var response = await _client.DeleteAsync($"/api/employees/{employee!.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEmployee_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/employees/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record EmployeeIdResponse(int Id);
}
