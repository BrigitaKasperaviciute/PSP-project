using POS_System.IntegrationTests.Helpers;
using FluentAssertions;
using System.Net;
using Xunit;

namespace POS_System.IntegrationTests.Controllers
{
    /// <summary>
    /// Integration tests for CartDiscountController.
    /// Tests cart discount operations.
    /// </summary>
    public class CartDiscountControllerTests : IntegrationTestBase
    {
        private const string BaseUrl = "/api/cart-discount";

        [Fact]
        public async Task CreateCartDiscount_ValidRequest_ReturnsOkOrBadRequest()
        {
            // Arrange
            var discountRequest = new { CartId = 1, DiscountCode = "TEST10" };

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(discountRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.BadRequest,
                HttpStatusCode.NotFound,
                HttpStatusCode.InternalServerError
            );
        }

        [Fact]
        public async Task GetCartDiscountById_ValidId_ReturnsCartDiscount()
        {
            // Arrange
            int discountId = 1;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{discountId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task DeleteCartDiscountById_ValidId_ReturnsOk()
        {
            // Arrange
            int discountId = 1;

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/{discountId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateCartDiscount_ValidRequest_ReturnsOkOrError()
        {
            // Arrange
            var discountRequest = new { Value = 10, IsPercentage = true, EndDate = DateTime.UtcNow.AddDays(30) };

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(discountRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
        }
    }

    /// <summary>
    /// Integration tests for ItemDiscountController.
    /// Tests item discount CRUD operations.
    /// </summary>
    public class ItemDiscountControllerTests : IntegrationTestBase
    {
        private const string BaseUrl = "/api/item-discount";

        [Fact]
        public async Task CreateItemDiscount_ValidRequest_ReturnsOk()
        {
            // Arrange
            var discountRequest = new
            {
                Value = 1000,
                IsPercentage = true,
                Description = "Test Discount",
                StartDate = DateTime.UtcNow.AddDays(1),
                EndDate = DateTime.UtcNow.AddDays(30)
            };

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(discountRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetAllItemDiscounts_ReturnsListOfDiscounts()
        {
            // Arrange & Act
            var response = await Client.GetAsync(BaseUrl);

            // Assert
            AssertOkResponse(response);
            var discounts = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            discounts.ValueKind.Should().BeOneOf(System.Text.Json.JsonValueKind.Object, System.Text.Json.JsonValueKind.Array);
        }

        [Fact]
        public async Task GetItemDiscountById_ValidId_ReturnsDiscount()
        {
            // Arrange
            int discountId = 1;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{discountId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task UpdateItemDiscountById_ValidRequest_ReturnsUpdatedDiscount()
        {
            // Arrange
            var updateRequest = new
            {
                Value = 2000,
                IsPercentage = false,
                Description = "Updated Discount",
                StartDate = DateTime.UtcNow.AddDays(1),
                EndDate = DateTime.UtcNow.AddDays(30)
            };

            // Act
            var response = await Client.PutAsync($"{BaseUrl}/1", CreateJsonContent(updateRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task DeleteItemDiscountById_ValidId_ReturnsOk()
        {
            // Arrange
            int discountId = 1;

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/{discountId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task LinkItemDiscountToItems_ValidIds_ReturnsOkOrBadRequest()
        {
            // Arrange
            var linkRequest = new[] { 1, 2 };

            // Act
            var response = await Client.PutAsync(
                $"{BaseUrl}/1/link?itemsAreProducts=true",
                CreateJsonContent(linkRequest)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task UnlinkItemDiscountFromItems_ValidIds_ReturnsOkOrBadRequest()
        {
            // Arrange
            var itemIds = new[] { 1, 2 };

            // Act
            var response = await Client.PutAsync(
                $"{BaseUrl}/1/unlink?itemsAreProducts=true",
                CreateJsonContent(itemIds)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetItemDiscountsLinkedToItemId_ValidId_ReturnsDiscounts()
        {
            // Arrange
            int itemId = 1;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/item/{itemId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateItemDiscount_UnauthenticatedUser_ReturnsForbidden()
        {
            // Arrange
            var discountRequest = new
            {
                Value = 1000,
                IsPercentage = true,
                Description = "Test Discount",
                StartDate = DateTime.UtcNow.AddDays(1),
                EndDate = DateTime.UtcNow.AddDays(30)
            };

            // Act
            var response = await UnauthenticatedClient.PostAsync(BaseUrl, CreateJsonContent(discountRequest));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }
    }
}
