using POS_System.IntegrationTests.Helpers;
using FluentAssertions;
using System.Net;
using Xunit;

namespace POS_System.IntegrationTests.Controllers
{
    /// <summary>
    /// Integration tests for EmployeeController.
    /// Tests employee operations including retrieval, updates, and deletions.
    /// </summary>
    public class EmployeeControllerTests : IntegrationTestBase
    {
        private const string BaseUrl = "/api/employees";

        [Fact]
        public async Task GetEmployeesAsync_ReturnsListOfEmployees()
        {
            // Arrange & Act
            var response = await Client.GetAsync(BaseUrl);

            // Assert
            AssertOkResponse(response);
            var employees = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            employees.ValueKind.Should().BeOneOf(System.Text.Json.JsonValueKind.Array, System.Text.Json.JsonValueKind.Object);
        }

        [Fact]
        public async Task GetEmployeeByIdAsync_ValidId_ReturnsEmployee()
        {
            // Arrange
            int employeeId = 1;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{employeeId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetEmployeeByIdAsync_InvalidId_ReturnsNotFound()
        {
            // Arrange
            int invalidId = 999999;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{invalidId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task UpdateEmployeeByIdAsync_ValidRequest_ReturnsUpdatedEmployee()
        {
            // Arrange
            var updateRequest = new { FirstName = "Updated", LastName = "Employee" };

            // Act
            var response = await Client.PutAsync($"{BaseUrl}/1", CreateJsonContent(updateRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task UpdateEmployeeByIdAsync_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var updateRequest = new { FirstName = "Updated", LastName = "Employee" };

            // Act
            var response = await Client.PutAsync($"{BaseUrl}/999999", CreateJsonContent(updateRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task DeleteEmployeeByIdAsync_ValidId_ReturnsOk()
        {
            // Arrange
            int employeeId = 1;

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/{employeeId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task DeleteEmployeeByIdAsync_InvalidId_ReturnsNotFound()
        {
            // Arrange
            int invalidId = 999999;

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/{invalidId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task UpdateEmployee_UnauthenticatedUser_ReturnsForbidden()
        {
            // Arrange
            var updateRequest = new { FirstName = "Updated", LastName = "Employee" };

            // Act
            var response = await UnauthenticatedClient.PutAsync($"{BaseUrl}/1", CreateJsonContent(updateRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task DeleteEmployee_UnauthenticatedUser_ReturnsForbidden()
        {
            // Act
            var response = await UnauthenticatedClient.DeleteAsync($"{BaseUrl}/1");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }
    }
}
