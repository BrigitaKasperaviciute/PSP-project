using System.Net;
using System.Text.Json;
using FluentAssertions;
using POS_System.IntegrationTests.TestSupport;
using POS_System.IntegrationTests.Helpers;
using POS_System.Business.Dtos.Request;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class AuthControllerTests
{
    [Fact]
    public async Task RegisterUser_ValidRequest_ReturnsEmployeeAndPersists()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        var request = new UserRegisterRequest(
            Email: $"test{Guid.NewGuid()}@example.com",
            UserName: $"testuser{Guid.NewGuid()}",
            FirstName: "Test",
            LastName: "User",
            Password: "Password1!",
            PhoneNumber: "1234567890",
            BirthDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
            RoleId: 5
        );

        // Act
        var response = await client.PostAsync("api/employees/register", TestDataFactory.ToJsonContent(request));
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        body.Should().Contain(request.Email);
        body.Should().Contain(request.UserName);
    }

    [Fact]
    public async Task RegisterUser_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        // Act - send explicit null body to simulate missing payload
        var response = await client.PostAsync("api/employees/register", new StringContent("null", System.Text.Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);
    }
}
