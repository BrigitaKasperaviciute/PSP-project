using System.Net;
using System.Text.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Helpers;
using Xunit;

namespace POS_System.IntegrationTests.Controllers
{
    public class BusinessDetailControllerTests : IntegrationTestBase
    {
        private const string BaseUrl = "/api/business-details";

        [Fact]
        public async Task GetBusinessDetails_SeededDetails_ReturnsOkWithPersistedBody()
        {
            // Arrange
            var expectedBusinessDetails = JsonSerializer.Deserialize<BusinessDetailsResponse>(await File.ReadAllTextAsync(Factory.BusinessDetailsFilePath));

            // Act
            var response = await Client.GetAsync(BaseUrl);

            // Assert
            AssertOkResponse(response);
            var actualBusinessDetails = await DeserializeResponseAsync<BusinessDetailsResponse>(response);
            actualBusinessDetails.Should().NotBeNull();
            actualBusinessDetails.Should().BeEquivalentTo(expectedBusinessDetails);
        }

        [Fact]
        public async Task GetBusinessDetails_UnauthenticatedUser_ReturnsUnauthorized()
        {
            // Arrange

            // Act
            var response = await UnauthenticatedClient.GetAsync(BaseUrl);

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task CreateBusinessDetails_ValidRequest_StoresBusinessDetailsAndReturnsOk()
        {
            // Arrange
            var request = new BusinessDetailsRequestBuilder()
                .WithBusinessName($"Biz {Guid.NewGuid():N}"[..Math.Min(20, $"Biz {Guid.NewGuid():N}".Length)])
                .WithBusinessEmail($"{Guid.NewGuid():N}@example.com")
                .WithBusinessPhone("+37061111111")
                .WithCountry("Lithuania")
                .WithCity("Kaunas")
                .WithStreet("Freedom Avenue")
                .WithHouseNumber(12)
                .WithFlatNumber(4)
                .Build();

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(request));

            // Assert
            AssertOkResponse(response);
            var responseDto = await DeserializeResponseAsync<BusinessDetailsResponse>(response);
            responseDto.Should().BeEquivalentTo(request);

            var storedBusinessDetails = JsonSerializer.Deserialize<BusinessDetailsResponse>(await File.ReadAllTextAsync(Factory.BusinessDetailsFilePath));
            storedBusinessDetails.Should().BeEquivalentTo(request);
        }

        [Fact]
        public async Task CreateBusinessDetails_UnauthenticatedUser_ReturnsUnauthorized()
        {
            // Arrange
            var request = BusinessDetailsRequestBuilder.CreateDefault();

            // Act
            var response = await UnauthenticatedClient.PostAsync(BaseUrl, CreateJsonContent(request));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task UpdateBusinessDetails_ValidRequest_OverwritesBusinessDetailsAndReturnsOk()
        {
            // Arrange
            var request = new BusinessDetailsRequestBuilder()
                .WithBusinessName("Updated Biz")
                .WithBusinessEmail("updated@example.com")
                .WithBusinessPhone("+37062222222")
                .WithCountry("Latvia")
                .WithCity("Riga")
                .WithStreet("Old Town Road")
                .WithHouseNumber(88)
                .WithFlatNumber(null)
                .Build();

            // Act
            var response = await Client.PutAsync(BaseUrl, CreateJsonContent(request));

            // Assert
            AssertOkResponse(response);
            var responseDto = await DeserializeResponseAsync<BusinessDetailsResponse>(response);
            responseDto.Should().BeEquivalentTo(request);

            var storedBusinessDetails = JsonSerializer.Deserialize<BusinessDetailsResponse>(await File.ReadAllTextAsync(Factory.BusinessDetailsFilePath));
            storedBusinessDetails.Should().BeEquivalentTo(request);
        }

        [Fact]
        public async Task UpdateBusinessDetails_UnauthenticatedUser_ReturnsUnauthorized()
        {
            // Arrange
            var request = BusinessDetailsRequestBuilder.CreateDefault();

            // Act
            var response = await UnauthenticatedClient.PutAsync(BaseUrl, CreateJsonContent(request));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }
    }
}