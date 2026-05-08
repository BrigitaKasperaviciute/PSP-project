using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using POS_System.Business.Dtos.Request;
using POS_System.Common.Exceptions;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class ProductModificationControllerTests : IClassFixture<ApiLayerTestApplicationFactory>
{
    private readonly ApiLayerTestApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProductModificationControllerTests(ApiLayerTestApplicationFactory factory)
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

    private const string ValidBody =
        "{\"productVersionId\":1,\"name\":\"Extra shot\",\"description\":\"Add espresso\",\"price\":50}";

    // --- GetAll ---

    [Fact]
    public async Task GetAll_DefaultParams_Returns200()
    {
        var response = await _client.GetAsync("/api/product-modification?pageSize=5&pageNumber=0");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetAll_ServiceThrowsInternalError_Returns500()
    {
        _factory.ProductModificationServiceMock
            .Setup(x => x.GetProductModificationsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var response = await _client.GetAsync("/api/product-modification?pageSize=5&pageNumber=0");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_ExistingModification_Returns200()
    {
        var response = await _client.GetAsync("/api/product-modification/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetById_NonExistingModification_Returns404()
    {
        _factory.ProductModificationServiceMock
            .Setup(x => x.GetProductModificationByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Product modification not found"));

        var response = await _client.GetAsync("/api/product-modification/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Create ---

    [Fact]
    public async Task Create_ValidRequest_Returns200()
    {
        var response = await _client.PostAsync("/api/product-modification", Json(ValidBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Create_ServiceThrowsBadRequest_Returns400()
    {
        _factory.ProductModificationServiceMock
            .Setup(x => x.CreateProductModificationAsync(It.IsAny<ProductModificationRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Product version not found"));

        var response = await _client.PostAsync("/api/product-modification", Json(ValidBody));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Update ---

    [Fact]
    public async Task Update_ExistingModification_Returns200()
    {
        var response = await _client.PutAsync("/api/product-modification/1", Json(ValidBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Update_NonExistingModification_Returns404()
    {
        _factory.ProductModificationServiceMock
            .Setup(x => x.UpdateProductModificationByIdAsync(It.IsAny<int>(), It.IsAny<ProductModificationRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Product modification not found"));

        var response = await _client.PutAsync("/api/product-modification/999", Json(ValidBody));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_ExistingModification_Returns200()
    {
        var response = await _client.DeleteAsync("/api/product-modification/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Delete_NonExistingModification_Returns404()
    {
        _factory.ProductModificationServiceMock
            .Setup(x => x.DeleteProductModificationByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Product modification not found"));

        var response = await _client.DeleteAsync("/api/product-modification/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- GetLinkedToCartItem ---

    [Fact]
    public async Task GetLinkedToCartItem_ValidCartItem_Returns200()
    {
        var response = await _client.GetAsync("/api/product-modification/cart-item/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetLinkedToCartItem_CartItemNotFound_Returns404()
    {
        _factory.ProductModificationServiceMock
            .Setup(x => x.GetProductModificationsLinkedToCartItemId(It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Cart item not found"));

        var response = await _client.GetAsync("/api/product-modification/cart-item/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- GetLinkedToProduct ---

    [Fact]
    public async Task GetLinkedToProduct_ValidProduct_Returns200()
    {
        var response = await _client.GetAsync("/api/product-modification/product/1?pageSize=5&pageNumber=0");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetLinkedToProduct_ProductNotFound_Returns404()
    {
        _factory.ProductModificationServiceMock
            .Setup(x => x.GetProductModificationsLinkedToProductId(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Product not found"));

        var response = await _client.GetAsync("/api/product-modification/product/999?pageSize=5&pageNumber=0");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
