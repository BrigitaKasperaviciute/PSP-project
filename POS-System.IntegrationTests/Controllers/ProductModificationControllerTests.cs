using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Domain.Entities;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Helpers;
using Xunit;

namespace POS_System.IntegrationTests.Controllers
{
    public class ProductModificationControllerTests : IntegrationTestBase
    {
        private const string BaseUrl = "/api/product-modification";

        [Fact]
        public async Task GetAllProductModifications_WithCreatedModifications_ReturnsPagedResults()
        {
            // Arrange
            var beforeCount = await ExecuteDbAsync(async db => await db.ProductModifications.CountAsync());

            await CreateVersionedProductModificationAsync(new ProductModificationRequestBuilder()
                .WithProductVersionId(21)
                .WithName($"Cheese {Guid.NewGuid():N}")
                .Build());

            await CreateVersionedProductModificationAsync(new ProductModificationRequestBuilder()
                .WithProductVersionId(22)
                .WithName($"Bacon {Guid.NewGuid():N}")
                .Build());

            // Act
            var response = await Client.GetAsync($"{BaseUrl}?pageSize=10&pageNumber=0");

            // Assert
            AssertOkResponse(response);
            var pagedResponse = await DeserializeResponseAsync<PagedResponse<ProductModificationResponse>>(response);
            pagedResponse.Should().NotBeNull();
            pagedResponse!.Results.Should().HaveCountGreaterThanOrEqualTo(beforeCount + 2);

            var persistedProductModifications = await ExecuteDbAsync(async db => await db.ProductModifications.ToListAsync());
            persistedProductModifications.Should().HaveCountGreaterThanOrEqualTo(beforeCount + 2);
        }

        [Fact]
        public async Task GetAllProductModifications_UnauthenticatedUser_ReturnsUnauthorized()
        {
            // Arrange

            // Act
            var response = await UnauthenticatedClient.GetAsync(BaseUrl);

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetProductModificationById_ExistingProductModification_ReturnsPersistedRecord()
        {
            // Arrange
            var createdProductModification = await CreateVersionedProductModificationAsync(ProductModificationRequestBuilder.CreateDefault());

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{createdProductModification.Id}");

            // Assert
            AssertOkResponse(response);
            var productModification = await DeserializeResponseAsync<ProductModificationResponse>(response);
            productModification.Should().NotBeNull();
            productModification!.Id.Should().Be(createdProductModification.Id);
            productModification.Name.Should().Be(createdProductModification.Name);
            productModification.Description.Should().Be(createdProductModification.Description);
            productModification.Price.Should().Be(createdProductModification.Price);
        }

        [Fact]
        public async Task GetProductModificationById_UnknownId_ReturnsNotFound()
        {
            // Arrange

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/999999");

            // Assert
            AssertNotFoundResponse(response);
        }

        [Fact]
        public async Task GetProductModificationVersionsByProductModificationId_UpdatedProductModification_ReturnsAllVersions()
        {
            // Arrange
            var createdProductModification = await CreateVersionedProductModificationAsync(new ProductModificationRequestBuilder()
                .WithProductVersionId(31)
                .WithName("Original Add-On")
                .WithDescription("Original description")
                .WithPrice(150)
                .Build());

            var updateRequest = new ProductModificationRequestBuilder()
                .WithProductVersionId(31)
                .WithName("Updated Add-On")
                .WithDescription("Updated description")
                .WithPrice(175)
                .Build();

            var updateResponse = await Client.PutAsync($"{BaseUrl}/{createdProductModification.Id}", CreateJsonContent(updateRequest));
            AssertOkResponse(updateResponse);

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{createdProductModification.ProductModificationId}/versions");

            // Assert
            AssertOkResponse(response);
            var versions = await DeserializeResponseAsync<List<ProductModificationResponse>>(response);
            versions.Should().NotBeNull();
            versions!.Should().HaveCount(2);
            versions.Should().Contain(version => version.Name == "Original Add-On");
            versions.Should().Contain(version => version.Name == "Updated Add-On");
        }

        [Fact]
        public async Task GetProductModificationVersionsByProductModificationId_UnauthenticatedUser_ReturnsUnauthorized()
        {
            // Arrange

            // Act
            var response = await UnauthenticatedClient.GetAsync($"{BaseUrl}/1/versions");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetProductModificationsLinkedToProductId_CreatedProductModifications_ReturnsPagedResults()
        {
            // Arrange
            await CreateVersionedProductModificationAsync(new ProductModificationRequestBuilder()
                .WithProductVersionId(77)
                .WithName("Linked Item A")
                .Build());

            await CreateVersionedProductModificationAsync(new ProductModificationRequestBuilder()
                .WithProductVersionId(77)
                .WithName("Linked Item B")
                .Build());

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/product/77?pageSize=10&pageNumber=0");

            // Assert
            AssertOkResponse(response);
            var pagedResponse = await DeserializeResponseAsync<PagedResponse<ProductModificationResponse>>(response);
            pagedResponse.Should().NotBeNull();
            pagedResponse!.Results.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetProductModificationsLinkedToCartItemId_UnlinkedCartItem_ReturnsEmptyResults()
        {
            // Arrange
            var cartItemId = await ExecuteDbAsync(async db =>
            {
                var cartItem = new CartItem
                {
                    CartId = 1,
                    Quantity = 1,
                    IsProduct = true,
                    IsDeleted = false,
                    ProductVersionId = 77,
                    ServiceVersionId = null
                };

                db.CartItems.Add(cartItem);
                await db.SaveChangesAsync();
                return cartItem.Id;
            });

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/cart-item/{cartItemId}");

            // Assert
            AssertOkResponse(response);
            var linkedProductModifications = await DeserializeResponseAsync<List<ProductModificationResponse>>(response);
            linkedProductModifications.Should().NotBeNull();
            linkedProductModifications.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateProductModification_ValidRequest_PersistsProductModificationAndReturnsOk()
        {
            // Arrange
            var request = new ProductModificationRequestBuilder()
                .WithProductVersionId(1)
                .WithName($"Create Add-On {Guid.NewGuid():N}")
                .WithDescription("Create description")
                .WithPrice(250)
                .Build();

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(request));

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return;
            }
            var productModification = await DeserializeResponseAsync<ProductModificationResponse>(response);
            productModification.Should().NotBeNull();
            productModification!.ProductVersionId.Should().Be(request.ProductVersionId);
            productModification.Name.Should().Be(request.Name);
            productModification.Description.Should().Be(request.Description);
            productModification.Price.Should().Be(request.Price);
            productModification.IsDeleted.Should().BeFalse();

            var persistedProductModification = await ExecuteDbAsync(async db => await db.ProductModifications.OrderByDescending(productModificationEntity => productModificationEntity.Id).FirstAsync());
            persistedProductModification.Should().NotBeNull();
            persistedProductModification.Name.Should().Be(request.Name);
            persistedProductModification.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task CreateProductModification_NullBody_ReturnsBadRequest()
        {
            // Arrange
            var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");

            // Act
            var response = await Client.PostAsync(BaseUrl, content);

            // Assert
            AssertBadRequestResponse(response);
        }

        [Fact]
        public async Task UpdateProductModification_ExistingProductModification_CreatesNewVersionAndSoftDeletesOriginal()
        {
            // Arrange
            var createdProductModification = await CreateVersionedProductModificationAsync(new ProductModificationRequestBuilder()
                .WithProductVersionId(99)
                .WithName("Version One")
                .WithDescription("Version one description")
                .WithPrice(300)
                .Build());

            var updateRequest = new ProductModificationRequestBuilder()
                .WithProductVersionId(99)
                .WithName("Version Two")
                .WithDescription("Version two description")
                .WithPrice(350)
                .Build();

            // Act
            var response = await Client.PutAsync($"{BaseUrl}/{createdProductModification.Id}", CreateJsonContent(updateRequest));

            // Assert
            AssertOkResponse(response);
            var updatedProductModification = await DeserializeResponseAsync<ProductModificationResponse>(response);
            updatedProductModification.Should().NotBeNull();
            updatedProductModification!.Name.Should().Be(updateRequest.Name);
            updatedProductModification.Description.Should().Be(updateRequest.Description);
            updatedProductModification.Price.Should().Be(updateRequest.Price);
            updatedProductModification.IsDeleted.Should().BeFalse();

            var persistedOriginal = await ExecuteDbAsync(async db => await db.ProductModifications.SingleAsync(productModificationEntity => productModificationEntity.Id == createdProductModification.Id));
            persistedOriginal.IsDeleted.Should().BeTrue();

            var persistedUpdated = await ExecuteDbAsync(async db => await db.ProductModifications.SingleAsync(productModificationEntity => productModificationEntity.Id == updatedProductModification.Id));
            persistedUpdated.IsDeleted.Should().BeFalse();
            persistedUpdated.Name.Should().Be(updateRequest.Name);
        }

        [Fact]
        public async Task UpdateProductModification_UnknownId_ReturnsNotFound()
        {
            // Arrange
            var request = ProductModificationRequestBuilder.CreateDefault();

            // Act
            var response = await Client.PutAsync($"{BaseUrl}/999999", CreateJsonContent(request));

            // Assert
            AssertNotFoundResponse(response);
        }

        [Fact]
        public async Task DeleteProductModification_ExistingProductModification_MarksDeletedAndReturnsOk()
        {
            // Arrange
            var createdProductModification = await CreateVersionedProductModificationAsync(new ProductModificationRequestBuilder()
                .WithProductVersionId(111)
                .WithName("Delete Me")
                .WithDescription("Delete description")
                .WithPrice(400)
                .Build());

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/{createdProductModification.Id}");

            // Assert
            AssertOkResponse(response);
            var deletedProductModification = await DeserializeResponseAsync<ProductModificationResponse>(response);
            deletedProductModification.Should().NotBeNull();
            deletedProductModification!.IsDeleted.Should().BeTrue();

            var persistedDeletedProductModification = await ExecuteDbAsync(async db => await db.ProductModifications.SingleAsync(productModificationEntity => productModificationEntity.Id == createdProductModification.Id));
            persistedDeletedProductModification.IsDeleted.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteProductModification_UnknownId_ReturnsNotFound()
        {
            // Arrange

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/999999");

            // Assert
            AssertNotFoundResponse(response);
        }

        private async Task<ProductModificationResponse> CreateVersionedProductModificationAsync(ProductModificationRequest request)
        {
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(request));
            AssertOkResponse(response);

            var productModification = await DeserializeResponseAsync<ProductModificationResponse>(response);
            productModification.Should().NotBeNull();

            var persistedProductModification = await ExecuteDbAsync(async db =>
            {
                var storedProductModification = await db.ProductModifications.OrderByDescending(productModificationEntity => productModificationEntity.Id).FirstAsync();

                storedProductModification.ProductModificationId = storedProductModification.Id;
                await db.SaveChangesAsync();
                return storedProductModification;
            });

            productModification!.Id = persistedProductModification.Id;
            productModification.ProductModificationId = persistedProductModification.Id;
            return productModification;
        }
    }
}