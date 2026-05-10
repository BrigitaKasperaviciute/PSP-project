using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Helpers;
using FluentAssertions;
using System.Net;
using Xunit;

namespace POS_System.IntegrationTests.Controllers
{
    /// <summary>
    /// Integration tests for GiftCardController.
    /// Tests gift card operations including create, retrieve, update, and delete.
    /// </summary>
    public class GiftCardControllerTests : IntegrationTestBase
    {
        private const string BaseUrl = "/api/giftcards";

        [Fact]
        public async Task CreateGiftCard_ValidRequest_ReturnsOk()
        {
            // Arrange
            var giftCardRequest = GiftCardRequestBuilder.CreateDefault();

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(giftCardRequest));

            // Assert
            AssertOkResponse(response);
            var created = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            created.ValueKind.Should().BeOneOf(System.Text.Json.JsonValueKind.Object, System.Text.Json.JsonValueKind.Array);
        }

        [Fact]
        public async Task CreateGiftCard_WithValue_ReturnsOkWithCorrectValue()
        {
            // Arrange
            var giftCardRequest = GiftCardRequestBuilder.CreateWithValue(10000);

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(giftCardRequest));

            // Assert
            AssertOkResponse(response);
            var created = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            created.GetProperty("value").GetInt32().Should().Be(10000);
        }

        [Fact]
        public async Task CreateGiftCard_ExpiredDate_ReturnsOkOrBadRequest()
        {
            // Arrange
            var giftCardRequest = GiftCardRequestBuilder.CreateExpired();

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(giftCardRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CreateGiftCard_ZeroValue_ReturnsOkOrBadRequest()
        {
            // Arrange
            var giftCardRequest = GiftCardRequestBuilder.CreateWithValue(0);

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(giftCardRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetAllGiftCards_ReturnsListOfGiftCards()
        {
            // Arrange & Act
            var response = await Client.GetAsync(BaseUrl);

            // Assert
            AssertOkResponse(response);
            var giftCards = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            giftCards.ValueKind.Should().BeOneOf(System.Text.Json.JsonValueKind.Object, System.Text.Json.JsonValueKind.Array);
        }

        [Fact]
        public async Task GetGiftCardById_ValidId_ReturnsGiftCard()
        {
            // Arrange
            var giftCardRequest = GiftCardRequestBuilder.CreateDefault();
            var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(giftCardRequest));
            var giftCardId = await ExtractIdFromResponseAsync(createResponse);

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{giftCardId}");

            // Assert
            AssertOkResponse(response);
            var giftCard = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            giftCard.ValueKind.Should().BeOneOf(System.Text.Json.JsonValueKind.Object, System.Text.Json.JsonValueKind.Array);
        }

        [Fact]
        public async Task GetGiftCardById_InvalidId_ReturnsNotFound()
        {
            // Arrange
            int invalidId = 999999;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{invalidId}");

            // Assert
            AssertNotFoundResponse(response);
        }

        [Fact]
        public async Task UpdateGiftCard_ValidRequest_ReturnsUpdatedGiftCard()
        {
            // Arrange
            var giftCardRequest = GiftCardRequestBuilder.CreateDefault();
            var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(giftCardRequest));
            var giftCardId = await ExtractIdFromResponseAsync(createResponse);

            var updatedRequest = GiftCardRequestBuilder.CreateWithValue(15000);

            // Act
            var response = await Client.PutAsync($"{BaseUrl}/{giftCardId}", CreateJsonContent(updatedRequest));

            // Assert
            AssertOkResponse(response);
            var updated = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            updated.GetProperty("value").GetInt32().Should().Be(15000);
        }

        [Fact]
        public async Task UpdateGiftCard_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var giftCardRequest = GiftCardRequestBuilder.CreateDefault();

            // Act
            var response = await Client.PutAsync($"{BaseUrl}/999999", CreateJsonContent(giftCardRequest));

            // Assert
            AssertNotFoundResponse(response);
        }

        [Fact]
        public async Task DeleteGiftCard_ValidId_ReturnsOk()
        {
            // Arrange
            var giftCardRequest = GiftCardRequestBuilder.CreateDefault();
            var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(giftCardRequest));
            var giftCardId = await ExtractIdFromResponseAsync(createResponse);

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/{giftCardId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task DeleteGiftCard_InvalidId_ReturnsNotFound()
        {
            // Arrange
            int invalidId = 999999;

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/{invalidId}");

            // Assert
            AssertNotFoundResponse(response);
        }

        [Fact]
        public async Task CreateGiftCard_UnauthenticatedUser_ReturnsForbidden()
        {
            // Arrange
            var giftCardRequest = GiftCardRequestBuilder.CreateDefault();

            // Act
            var response = await UnauthenticatedClient.PostAsync(BaseUrl, CreateJsonContent(giftCardRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task UpdateGiftCard_UnauthenticatedUser_ReturnsForbidden()
        {
            // Arrange
            var giftCardRequest = GiftCardRequestBuilder.CreateDefault();

            // Act
            var response = await UnauthenticatedClient.PutAsync($"{BaseUrl}/1", CreateJsonContent(giftCardRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task DeleteGiftCard_UnauthenticatedUser_ReturnsForbidden()
        {
            // Act
            var response = await UnauthenticatedClient.DeleteAsync($"{BaseUrl}/1");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task CreateMultipleGiftCards_AllCreatedSuccessfully()
        {
            // Arrange
            var giftCards = new[]
            {
                GiftCardRequestBuilder.CreateWithValue(5000),
                GiftCardRequestBuilder.CreateWithValue(10000),
                GiftCardRequestBuilder.CreateWithValue(20000)
            };

            // Act & Assert
            var createdIds = new List<int>();
            foreach (var giftCard in giftCards)
            {
                var response = await Client.PostAsync(BaseUrl, CreateJsonContent(giftCard));
                AssertOkResponse(response);
                var id = await ExtractIdFromResponseAsync(response);
                createdIds.Add(id);
            }

            createdIds.Should().HaveCount(3);
            createdIds.Distinct().Should().HaveCount(3);
        }

        [Fact]
        public async Task DeleteGiftCard_ThenGetById_ReturnsNotFound()
        {
            // Arrange
            var giftCardRequest = GiftCardRequestBuilder.CreateDefault();
            var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(giftCardRequest));
            var giftCardId = await ExtractIdFromResponseAsync(createResponse);

            // Act
            var deleteResponse = await Client.DeleteAsync($"{BaseUrl}/{giftCardId}");
            deleteResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

            var getResponse = await Client.GetAsync($"{BaseUrl}/{giftCardId}");

            // Assert
            AssertNotFoundResponse(getResponse);
        }
    }
}
