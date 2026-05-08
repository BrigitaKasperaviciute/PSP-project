using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using POS_System.Business.Dtos.Request;
using POS_System.Common.Exceptions;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class CartItemControllerTests : IClassFixture<ApiLayerTestApplicationFactory>
{
    private readonly ApiLayerTestApplicationFactory _factory;
    private readonly HttpClient _client;

    public CartItemControllerTests(ApiLayerTestApplicationFactory factory)
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

    private const string ValidItemBody =
        "{\"cartId\":1,\"quantity\":2,\"isProduct\":true,\"productVersionId\":1,\"serviceVersionId\":null}";

    // --- GetAll ---

    [Fact]
    public async Task GetAll_ValidCart_Returns200()
    {
        var response = await _client.GetAsync("/api/carts/1/items?pageNum=0&pageSize=5");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetAll_ServiceThrowsNotFound_Returns404()
    {
        _factory.CartItemServiceMock
            .Setup(x => x.GetAllCartItemsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<int>()))
            .ThrowsAsync(new NotFoundException("Cart not found"));

        var response = await _client.GetAsync("/api/carts/999/items?pageNum=0&pageSize=5");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Create ---

    [Fact]
    public async Task Create_ValidRequest_Returns200()
    {
        var response = await _client.PostAsync("/api/carts/1/items", Json(ValidItemBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Create_ServiceThrowsBadRequest_Returns400()
    {
        _factory.CartItemServiceMock
            .Setup(x => x.CreateCartItemAsync(It.IsAny<CartItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Product version not found"));

        var response = await _client.PostAsync("/api/carts/1/items", Json(ValidItemBody));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Update ---

    [Fact]
    public async Task Update_ExistingItem_Returns200()
    {
        var response = await _client.PutAsync("/api/carts/1/items/1", Json(ValidItemBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Update_NonExistingItem_Returns404()
    {
        _factory.CartItemServiceMock
            .Setup(x => x.UpdateCartItemAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CartItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Cart item not found"));

        var response = await _client.PutAsync("/api/carts/1/items/999", Json(ValidItemBody));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_ExistingItem_ReturnsSuccess()
    {
        var response = await _client.DeleteAsync("/api/carts/1/items/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Delete_NonExistingItem_Returns404()
    {
        _factory.CartItemServiceMock
            .Setup(x => x.DeleteCartItemAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Cart item not found"));

        var response = await _client.DeleteAsync("/api/carts/1/items/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Link ---

    [Fact]
    public async Task Link_ValidModifications_Returns200()
    {
        var response = await _client.PutAsync("/api/carts/1/items/1/link", Json("[1,2]"));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Link_ServiceThrowsBadRequest_Returns400()
    {
        _factory.CartItemServiceMock
            .Setup(x => x.LinkCartItemToProductModificationsAsync(It.IsAny<int>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Modification already linked"));

        var response = await _client.PutAsync("/api/carts/1/items/1/link", Json("[1,2]"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Unlink ---

    [Fact]
    public async Task Unlink_ValidModifications_Returns200()
    {
        var response = await _client.PutAsync("/api/carts/1/items/1/unlink", Json("[1]"));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Unlink_ServiceThrowsNotFound_Returns404()
    {
        _factory.CartItemServiceMock
            .Setup(x => x.UnlinkCartItemFromProductModificationsAsync(It.IsAny<int>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Modification not linked"));

        var response = await _client.PutAsync("/api/carts/1/items/1/unlink", Json("[99]"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
