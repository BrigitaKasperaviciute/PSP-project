using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using POS_System.Business.Dtos.Request;
using POS_System.Common.Exceptions;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class CartDiscountControllerTests : IClassFixture<ApiLayerTestApplicationFactory>
{
    private readonly ApiLayerTestApplicationFactory _factory;
    private readonly HttpClient _client;

    public CartDiscountControllerTests(ApiLayerTestApplicationFactory factory)
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
        "{\"value\":10,\"isPercentage\":true,\"endDate\":\"2030-01-01T00:00:00Z\"}";

    // --- Create ---

    [Fact]
    public async Task Create_ValidRequest_Returns200()
    {
        var response = await _client.PostAsync("/api/cart-discount", Json(ValidBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Create_ServiceThrowsConflict_Returns409()
    {
        _factory.CartDiscountServiceMock
            .Setup(x => x.CreateCartDiscountAsync(It.IsAny<CartDiscountRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("Discount code already exists"));

        var response = await _client.PostAsync("/api/cart-discount", Json(ValidBody));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_ExistingDiscount_Returns200()
    {
        var response = await _client.GetAsync("/api/cart-discount/DISC10");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetById_NonExistingDiscount_Returns404()
    {
        _factory.CartDiscountServiceMock
            .Setup(x => x.GetCartDiscountByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Cart discount not found"));

        var response = await _client.GetAsync("/api/cart-discount/NONEXISTENT");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_ExistingDiscount_Returns200()
    {
        var response = await _client.DeleteAsync("/api/cart-discount/DISC10");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Delete_NonExistingDiscount_Returns404()
    {
        _factory.CartDiscountServiceMock
            .Setup(x => x.DeleteCartDiscountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Cart discount not found"));

        var response = await _client.DeleteAsync("/api/cart-discount/NONEXISTENT");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
