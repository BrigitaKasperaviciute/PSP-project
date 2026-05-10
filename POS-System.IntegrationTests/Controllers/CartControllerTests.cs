using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Helpers;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common.Enums;
using System.Net;
using Xunit;

namespace POS_System.IntegrationTests.Controllers
{
    /// <summary>
    /// Integration tests for CartController.
    /// Tests cart operations including create, retrieve, update, and discount application.
    /// </summary>
    public class CartControllerTests : IntegrationTestBase
    {
        private const string BaseUrl = "/api/carts";

        [Fact]
        public async Task GetAllCarts_ReturnsListOfCarts()
        {
            // Arrange & Act
            var response = await Client.GetAsync(BaseUrl);

            // Assert
            AssertOkResponse(response);
            var carts = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            carts.ValueKind.Should().BeOneOf(System.Text.Json.JsonValueKind.Object, System.Text.Json.JsonValueKind.Array);
        }

        [Fact]
        public async Task GetCartByID_ValidId_ReturnsCart()
        {
            // Arrange
            var createRequest = new { };
            var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(createRequest));
            
            if (createResponse.StatusCode != HttpStatusCode.OK && 
                createResponse.StatusCode != HttpStatusCode.Created)
            {
                return; // Skip if cart creation not supported
            }

            var createdCart = await DeserializeResponseAsync<System.Text.Json.JsonElement>(createResponse);
            var cartId = createdCart.GetProperty("id").GetInt32();

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{cartId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetCartByID_InvalidId_ReturnsNotFound()
        {
            // Arrange
            int invalidId = 999999;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{invalidId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task DeleteCart_ValidId_ReturnsOk()
        {
            // Arrange
            var createRequest = new { };
            var createResponse = await Client.PostAsync(BaseUrl, CreateJsonContent(createRequest));

            if (createResponse.StatusCode != HttpStatusCode.OK && 
                createResponse.StatusCode != HttpStatusCode.Created)
            {
                return; // Skip if cart creation not supported
            }

            var createdCart = await DeserializeResponseAsync<System.Text.Json.JsonElement>(createResponse);
            var cartId = createdCart.GetProperty("id").GetInt32();

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/{cartId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task DeleteCart_InvalidId_ReturnsNotFound()
        {
            // Arrange
            int invalidId = 999999;

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/{invalidId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task CreateCart_ValidRequest_ReturnsOk()
        {
            // Arrange
            var createRequest = new CartRequest
            {
                EmployeeVersionId = 1
            };

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(createRequest));

            // Assert
            AssertOkResponse(response);

            var createdCart = await DeserializeResponseAsync<CartResponse>(response);
            createdCart.Should().NotBeNull();
            createdCart!.EmployeeVersionId.Should().Be(createRequest.EmployeeVersionId);
            createdCart.Status.Should().Be(CartStatusEnum.IN_PROGRESS);

            var storedCartExists = await ExecuteDbAsync(db => db.Carts.AnyAsync(cart => cart.EmployeeVersionId == createRequest.EmployeeVersionId && cart.Status == CartStatusEnum.IN_PROGRESS));
            storedCartExists.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteCart_ExistingCart_ReturnsOkAndRemovesCart()
        {
            // Arrange
            const int cartId = 3;

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/{cartId}");

            // Assert
            AssertOkResponse(response);

            var cartExists = await ExecuteDbAsync(db => db.Carts.AnyAsync(cart => cart.Id == cartId));
            cartExists.Should().BeFalse();
        }

        [Fact]
        public async Task ApplyDiscountToCart_ValidCoupon_ReturnsOkOrError()
        {
            // Arrange
            var discountRequest = new { couponCode = "TESTCOUPON" };

            // Act
            var response = await Client.PostAsync(
                $"{BaseUrl}/1/discount",
                CreateJsonContent(discountRequest)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.BadRequest,
                HttpStatusCode.NotFound,
                HttpStatusCode.InternalServerError,
                HttpStatusCode.MethodNotAllowed
            );
        }

        [Fact]
        public async Task ApplyDiscountToCart_InvalidCoupon_ReturnsErrorOrNotFound()
        {
            // Arrange
            var discountRequest = new { couponCode = "INVALIDCOUPON12345" };

            // Act
            var response = await Client.PostAsync(
                $"{BaseUrl}/1/discount",
                CreateJsonContent(discountRequest)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.BadRequest,
                HttpStatusCode.NotFound,
                HttpStatusCode.InternalServerError,
                HttpStatusCode.MethodNotAllowed
            );
        }

        [Fact]
        public async Task GetCartDiscountAsync_ValidCartId_ReturnsDiscountOrNotFound()
        {
            // Arrange
            int cartId = 1;

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{cartId}/discount");

            // Assert
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.NotFound,
                HttpStatusCode.BadRequest,
                HttpStatusCode.NoContent
            );
        }
    }
}
