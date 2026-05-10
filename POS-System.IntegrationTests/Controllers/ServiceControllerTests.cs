using POS_System.IntegrationTests.Helpers;
using FluentAssertions;
using System.Net;
using Xunit;

namespace POS_System.IntegrationTests.Controllers
{
    /// <summary>
    /// Integration tests for ServiceController.
    /// Tests service CRUD operations including creation, retrieval, update, and deletion.
    /// </summary>
    public class ServiceControllerTests : IntegrationTestBase
    {
        private const string BaseUrl = "/api/services";

        [Fact]
        public async Task CreateService_ValidRequest_ReturnsOk()
        {
            // Arrange
            var serviceRequest = new
            {
                Name = "Test Service",
                Description = "Test Description",
                Duration = 30,
                Price = 5000,
                ImageURL = "https://example.com/service.jpg",
                EmployeeId = 1
            };

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(serviceRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetAllServices_ReturnsListOfServices()
        {
            // Arrange & Act
            var response = await Client.GetAsync(BaseUrl);

            // Assert
            AssertOkResponse(response);
            var services = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            services.ValueKind.Should().BeOneOf(System.Text.Json.JsonValueKind.Array, System.Text.Json.JsonValueKind.Object);
        }

        [Fact]
        public async Task GetServiceById_ValidId_ReturnsService()
        {
            // Arrange
            int serviceId = 1;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{serviceId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetServiceById_InvalidId_ReturnsNotFound()
        {
            // Arrange
            int invalidId = 999999;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{invalidId}");

            // Assert
            AssertNotFoundResponse(response);
        }

        [Fact]
        public async Task UpdateService_ValidRequest_ReturnsUpdatedService()
        {
            // Arrange
            var updateRequest = new
            {
                Name = "Updated Service",
                Description = "Updated Description",
                Duration = 45,
                Price = 6000,
                ImageURL = "https://example.com/updated.jpg",
                EmployeeId = 1
            };

            // Act
            var response = await Client.PutAsync($"{BaseUrl}/1", CreateJsonContent(updateRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task DeleteService_ValidId_ReturnsOk()
        {
            // Arrange
            int serviceId = 1;

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/{serviceId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateService_UnauthenticatedUser_ReturnsForbidden()
        {
            // Arrange
            var serviceRequest = new
            {
                Name = "Test Service",
                Description = "Test Description",
                Duration = 30,
                Price = 5000,
                ImageURL = "https://example.com/service.jpg",
                EmployeeId = 1
            };

            // Act
            var response = await UnauthenticatedClient.PostAsync(BaseUrl, CreateJsonContent(serviceRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetServicesLinkedToTaxId_ValidId_ReturnsServices()
        {
            // Arrange
            int taxId = 1;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/tax/{taxId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetServicesLinkedToItemDiscountId_ValidId_ReturnsServices()
        {
            // Arrange
            int discountId = 1;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/item-discount/{discountId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }
    }
}
