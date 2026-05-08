using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using POS_System.Business.Dtos.Request;
using POS_System.Common.Exceptions;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class CartControllerTests : IClassFixture<ApiLayerTestApplicationFactory>
{
    private readonly ApiLayerTestApplicationFactory _factory;
    private readonly HttpClient _client;

    public CartControllerTests(ApiLayerTestApplicationFactory factory)
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

    // --- GetAll ---

    [Fact]
    public async Task GetAll_DefaultParams_Returns200()
    {
        var response = await _client.GetAsync("/api/carts?pageNum=0&pageSize=5");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetAll_ServiceThrowsInternalError_Returns500()
    {
        _factory.CartServiceMock
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        var response = await _client.GetAsync("/api/carts?pageNum=0&pageSize=5");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // --- Create ---

    [Fact]
    public async Task Create_ValidRequest_Returns200()
    {
        var response = await _client.PostAsync("/api/carts",
            Json("{\"employeeVersionId\":1}"));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Create_ServiceThrowsBadRequest_Returns400()
    {
        _factory.CartServiceMock
            .Setup(x => x.CreateCartAsync(It.IsAny<CartRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Employee not found"));

        var response = await _client.PostAsync("/api/carts",
            Json("{\"employeeVersionId\":99}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_ExistingCart_Returns200()
    {
        var response = await _client.DeleteAsync("/api/carts/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Delete_NonExistingCart_Returns404()
    {
        _factory.CartServiceMock
            .Setup(x => x.DeleteCartAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Cart not found"));

        var response = await _client.DeleteAsync("/api/carts/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- ApplyDiscount ---

    [Fact]
    public async Task ApplyDiscount_ValidCode_Returns200()
    {
        var response = await _client.PatchAsync("/api/carts/1/discount",
            Json("{\"discountCode\":\"DISC10\"}"));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ApplyDiscount_InvalidCode_Returns404()
    {
        _factory.CartServiceMock
            .Setup(x => x.ApplyDiscountForCartAsync(It.IsAny<int>(), It.IsAny<ApplyDiscountRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Discount code not found"));

        var response = await _client.PatchAsync("/api/carts/1/discount",
            Json("{\"discountCode\":\"INVALID\"}"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- GetCartDiscount ---

    [Fact]
    public async Task GetCartDiscount_CartWithDiscount_Returns200()
    {
        var response = await _client.GetAsync("/api/carts/1/discount");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetCartDiscount_CartNotFound_Returns404()
    {
        _factory.CartServiceMock
            .Setup(x => x.GetCartDiscountAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Cart not found"));

        var response = await _client.GetAsync("/api/carts/999/discount");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
