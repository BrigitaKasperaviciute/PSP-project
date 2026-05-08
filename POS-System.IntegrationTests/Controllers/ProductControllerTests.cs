using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using POS_System.Business.Dtos.Request;
using POS_System.Common.Exceptions;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class ProductControllerTests : IClassFixture<ApiLayerTestApplicationFactory>
{
    private readonly ApiLayerTestApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProductControllerTests(ApiLayerTestApplicationFactory factory)
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
        "{\"name\":\"Coffee\",\"description\":\"Hot drink\",\"price\":300,\"imageURL\":\"https://example.com/img.png\",\"stock\":50}";

    // --- GetAll ---

    [Fact]
    public async Task GetAll_DefaultParams_Returns200()
    {
        var response = await _client.GetAsync("/api/product?pageSize=5&pageNumber=0");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetAll_ServiceThrowsInternalError_Returns500()
    {
        _factory.ProductServiceMock
            .Setup(x => x.GetProductsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        var response = await _client.GetAsync("/api/product?pageSize=5&pageNumber=0");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_ExistingProduct_Returns200()
    {
        var response = await _client.GetAsync("/api/product/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetById_NonExistingProduct_Returns404()
    {
        _factory.ProductServiceMock
            .Setup(x => x.GetProductByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Product not found"));

        var response = await _client.GetAsync("/api/product/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- GetVersions ---

    [Fact]
    public async Task GetVersions_ExistingProduct_Returns200()
    {
        var response = await _client.GetAsync("/api/product/1/versions/");

        Assert.True(response.IsSuccessStatusCode);
    }

    // --- Create ---

    [Fact]
    public async Task Create_ValidRequest_Returns200()
    {
        var response = await _client.PostAsync("/api/product", Json(ValidBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Create_ServiceThrowsConflict_Returns409()
    {
        _factory.ProductServiceMock
            .Setup(x => x.CreateProductAsync(It.IsAny<ProductRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("Product already exists"));

        var response = await _client.PostAsync("/api/product", Json(ValidBody));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // --- Update ---

    [Fact]
    public async Task Update_ExistingProduct_Returns200()
    {
        var response = await _client.PutAsync("/api/product/1", Json(ValidBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Update_NonExistingProduct_Returns404()
    {
        _factory.ProductServiceMock
            .Setup(x => x.UpdateProductByIdAsync(It.IsAny<int>(), It.IsAny<ProductRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Product not found"));

        var response = await _client.PutAsync("/api/product/999", Json(ValidBody));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_ExistingProduct_Returns200()
    {
        var response = await _client.DeleteAsync("/api/product/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Delete_NonExistingProduct_Returns404()
    {
        _factory.ProductServiceMock
            .Setup(x => x.DeleteProductByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Product not found"));

        var response = await _client.DeleteAsync("/api/product/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- GetLinkedToTax ---

    [Fact]
    public async Task GetLinkedToTax_ValidTax_Returns200()
    {
        var response = await _client.GetAsync("/api/product/tax/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetLinkedToTax_TaxNotFound_Returns404()
    {
        _factory.ProductServiceMock
            .Setup(x => x.GetProductsLinkedToTaxId(It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Tax not found"));

        var response = await _client.GetAsync("/api/product/tax/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- GetLinkedToItemDiscount ---

    [Fact]
    public async Task GetLinkedToItemDiscount_ValidDiscount_Returns200()
    {
        var response = await _client.GetAsync("/api/product/item-discount/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetLinkedToItemDiscount_DiscountNotFound_Returns404()
    {
        _factory.ProductServiceMock
            .Setup(x => x.GetProductsLinkedToItemDiscountId(It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Item discount not found"));

        var response = await _client.GetAsync("/api/product/item-discount/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
