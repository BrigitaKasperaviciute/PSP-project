using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using POS_System.Business.Dtos.Request;
using POS_System.Common.Exceptions;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class EmployeeControllerTests : IClassFixture<ApiLayerTestApplicationFactory>
{
    private readonly ApiLayerTestApplicationFactory _factory;
    private readonly HttpClient _client;

    public EmployeeControllerTests(ApiLayerTestApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });
    }

    private static StringContent Json(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private const string ValidUpdateBody =
        "{\"firstName\":\"Jane\",\"lastName\":\"Doe\",\"birthDate\":\"1990-01-01\",\"userName\":\"jdoe\",\"email\":\"jdoe@example.com\",\"phoneNumber\":\"+37060000001\",\"roleId\":2}";

    // --- GetAll ---

    [Fact]
    public async Task GetAll_DefaultParams_Returns200()
    {
        var response = await _client.GetAsync("/api/employees?pageSize=5&pageNumber=0");

        Assert.True(response.IsSuccessStatusCode);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_ExistingEmployee_Returns200()
    {
        var response = await _client.GetAsync("/api/employees/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    // --- Update ---

    [Fact]
    public async Task Update_ExistingEmployee_Returns200()
    {
        var response = await _client.PutAsync("/api/employees/1", Json(ValidUpdateBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_ExistingEmployee_Returns200()
    {
        var response = await _client.DeleteAsync("/api/employees/1");

        Assert.True(response.IsSuccessStatusCode);
    }
}
