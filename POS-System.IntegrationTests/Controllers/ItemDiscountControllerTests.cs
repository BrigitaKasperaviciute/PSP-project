using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using POS_System.Business.Dtos.Request;
using POS_System.Common.Exceptions;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class ItemDiscountControllerTests : IClassFixture<ApiLayerTestApplicationFactory>
{
    private readonly ApiLayerTestApplicationFactory _factory;
    private readonly HttpClient _client;

    public ItemDiscountControllerTests(ApiLayerTestApplicationFactory factory)
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
        "{\"value\":10,\"isPercentage\":true,\"description\":\"Summer sale\",\"startDate\":null,\"endDate\":null}";

    // --- GetById ---

    [Fact]
    public async Task GetById_ExistingDiscount_Returns200()
    {
        var response = await _client.GetAsync("/api/item-discount/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetById_NonExistingDiscount_Returns404()
    {
        _factory.ItemDiscountServiceMock
            .Setup(x => x.GetItemDiscountByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Item discount not found"));

        var response = await _client.GetAsync("/api/item-discount/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Create ---

    [Fact]
    public async Task Create_ValidRequest_Returns200()
    {
        var response = await _client.PostAsync("/api/item-discount", Json(ValidBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Create_ServiceThrowsConflict_Returns409()
    {
        _factory.ItemDiscountServiceMock
            .Setup(x => x.CreateItemDiscountAsync(It.IsAny<ItemDiscountRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("Discount already exists"));

        var response = await _client.PostAsync("/api/item-discount", Json(ValidBody));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // --- Update ---

    [Fact]
    public async Task Update_ExistingDiscount_Returns200()
    {
        var response = await _client.PutAsync("/api/item-discount/1", Json(ValidBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Update_NonExistingDiscount_Returns404()
    {
        _factory.ItemDiscountServiceMock
            .Setup(x => x.UpdateItemDiscountAsync(It.IsAny<int>(), It.IsAny<ItemDiscountRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Item discount not found"));

        var response = await _client.PutAsync("/api/item-discount/999", Json(ValidBody));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_ExistingDiscount_Returns200()
    {
        var response = await _client.DeleteAsync("/api/item-discount/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Delete_NonExistingDiscount_Returns404()
    {
        _factory.ItemDiscountServiceMock
            .Setup(x => x.DeleteItemDiscountAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Item discount not found"));

        var response = await _client.DeleteAsync("/api/item-discount/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Link ---

    [Fact]
    public async Task Link_ValidItems_Returns200()
    {
        var response = await _client.PutAsync("/api/item-discount/1/link?itemsAreProducts=true",
            Json("[1,2]"));

        Assert.True(response.IsSuccessStatusCode);
    }

    // --- Unlink ---

    [Fact]
    public async Task Unlink_ValidItems_Returns200()
    {
        var response = await _client.PutAsync("/api/item-discount/1/unlink?itemsAreProducts=true",
            Json("[1]"));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Unlink_ServiceThrowsNotFound_Returns404()
    {
        _factory.ItemDiscountServiceMock
            .Setup(x => x.UnlinkItemDiscountFromItemsAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Discount or item not found"));

        var response = await _client.PutAsync("/api/item-discount/999/unlink?itemsAreProducts=true",
            Json("[1]"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- GetLinkedToItem ---

    [Fact]
    public async Task GetLinkedToItem_ValidItem_Returns200()
    {
        var response = await _client.GetAsync("/api/item-discount/item/1?isProduct=true");

        Assert.True(response.IsSuccessStatusCode);
    }
}
