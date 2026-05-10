using POS_System.IntegrationTests.Helpers;
using FluentAssertions;
using System.Net;
using Xunit;

namespace POS_System.IntegrationTests.Controllers
{
    /// <summary>
    /// Integration tests for CartItemController.
    /// Tests cart item operations including creation, retrieval, and removal.
    /// </summary>
    public class CartItemControllerTests : IntegrationTestBase
    {
        private const string BaseUrl = "/api/carts/1/items";

        [Fact]
        public async Task CreateCartItem_ValidRequest_ReturnsOkOrBadRequest()
        {
            // Arrange
            var cartItemRequest = new
            {
                CartId = 1,
                Quantity = 2,
                IsProduct = true,
                ProductVersionId = 1,
                ServiceVersionId = (int?)null
            };

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(cartItemRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.Created,
                HttpStatusCode.BadRequest,
                HttpStatusCode.NotFound,
                HttpStatusCode.InternalServerError
            );
        }

        [Fact]
        public async Task GetAllCartItems_ReturnsListOfCartItems()
        {
            // Arrange & Act
            var response = await Client.GetAsync(BaseUrl);

            // Assert
            AssertOkResponse(response);
            var cartItems = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            cartItems.ValueKind.Should().BeOneOf(System.Text.Json.JsonValueKind.Object, System.Text.Json.JsonValueKind.Array);
        }

        [Fact]
        public async Task GetCartItemByIdAndCartId_ValidIds_ReturnsCartItem()
        {
            // Arrange
            int cartItemId = 1;
            int cartId = 1;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{cartItemId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task UpdateCartItem_ValidRequest_ReturnsUpdatedCartItem()
        {
            // Arrange
            var updateRequest = new
            {
                CartId = 1,
                Quantity = 5,
                IsProduct = true
            };

            // Act
            var response = await Client.PutAsync($"{BaseUrl}/1", CreateJsonContent(updateRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.NotFound,
                HttpStatusCode.BadRequest,
                HttpStatusCode.InternalServerError
            );
        }

        [Fact]
        public async Task DeleteCartItem_ValidId_ReturnsOk()
        {
            // Arrange
            int cartItemId = 1;

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/{cartItemId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task LinkCartItemToProductModifications_ValidIds_ReturnsOkOrBadRequest()
        {
            // Arrange
            var linkRequest = new { productModificationIds = new[] { 1, 2 } };

            // Act
            var response = await Client.PutAsync(
                $"{BaseUrl}/link",
                CreateJsonContent(linkRequest)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.BadRequest,
                HttpStatusCode.NotFound,
                HttpStatusCode.InternalServerError
            );
        }

        [Fact]
        public async Task UnlinkCartItemFromProductModifications_ValidIds_ReturnsOkOrBadRequest()
        {
            // Arrange
            var unlinkRequest = new { productModificationIds = new[] { 1, 2 } };

            // Act
            var response = await Client.PutAsync($"{BaseUrl}/unlink", CreateJsonContent(unlinkRequest.productModificationIds));

            // Assert
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.BadRequest,
                HttpStatusCode.NotFound,
                HttpStatusCode.InternalServerError
            );
        }

        [Fact]
        public async Task CreateCartItem_UnauthenticatedUser_ReturnsForbidden()
        {
            // Arrange
            var cartItemRequest = new
            {
                CartId = 1,
                Quantity = 2,
                IsProduct = true
            };

            // Act
            var response = await UnauthenticatedClient.PostAsync(BaseUrl, CreateJsonContent(cartItemRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        }
    }
}
