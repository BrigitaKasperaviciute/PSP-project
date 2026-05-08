using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using POS_System.Business.Dtos.Request;
using POS_System.Common.Exceptions;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class TaxControllerTests : IClassFixture<ApiLayerTestApplicationFactory>
{
    private readonly ApiLayerTestApplicationFactory _factory;
    private readonly HttpClient _client;

    public TaxControllerTests(ApiLayerTestApplicationFactory factory)
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
        "{\"name\":\"VAT\",\"rate\":21,\"isPercentage\":true}";

    // --- GetAll ---

    [Fact]
    public async Task GetAll_DefaultParams_Returns200()
    {
        var response = await _client.GetAsync("/api/tax?pageNum=0&pageSize=5");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetAll_ServiceThrowsInternalError_Returns500()
    {
        _factory.TaxServiceMock
            .Setup(x => x.GetAllTaxesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var response = await _client.GetAsync("/api/tax?pageNum=0&pageSize=5");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_ExistingTax_Returns200()
    {
        var response = await _client.GetAsync("/api/tax/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetById_NonExistingTax_Returns404()
    {
        _factory.TaxServiceMock
            .Setup(x => x.GetTaxByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Tax not found"));

        var response = await _client.GetAsync("/api/tax/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Create ---

    [Fact]
    public async Task Create_ValidRequest_Returns200()
    {
        var response = await _client.PostAsync("/api/tax", Json(ValidBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Create_ServiceThrowsConflict_Returns409()
    {
        _factory.TaxServiceMock
            .Setup(x => x.CreateTaxAsync(It.IsAny<TaxRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("Tax name already exists"));

        var response = await _client.PostAsync("/api/tax", Json(ValidBody));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // --- Update ---

    [Fact]
    public async Task Update_ExistingTax_Returns200()
    {
        var response = await _client.PutAsync("/api/tax/1", Json(ValidBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Update_NonExistingTax_Returns404()
    {
        _factory.TaxServiceMock
            .Setup(x => x.UpdateTaxAsync(It.IsAny<int>(), It.IsAny<TaxRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Tax not found"));

        var response = await _client.PutAsync("/api/tax/999", Json(ValidBody));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_ExistingTax_Returns200()
    {
        var response = await _client.DeleteAsync("/api/tax/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Delete_NonExistingTax_Returns404()
    {
        _factory.TaxServiceMock
            .Setup(x => x.DeleteTaxAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Tax not found"));

        var response = await _client.DeleteAsync("/api/tax/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Link_TaxOrItemNotFound_Returns404()
    {
        _factory.TaxServiceMock
            .Setup(x => x.LinkTaxToItemsAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Tax or item not found"));

        var response = await _client.PutAsync("/api/tax/999/link?itemsAreProducts=true",
            Json("[1,2]"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Unlink ---

    [Fact]
    public async Task Unlink_ValidItems_Returns200()
    {
        var response = await _client.PutAsync("/api/tax/1/unlink?itemsAreProducts=true",
            Json("[1]"));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Unlink_TaxOrItemNotFound_Returns404()
    {
        _factory.TaxServiceMock
            .Setup(x => x.UnlinkTaxFromItemsAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Tax or item not found"));

        var response = await _client.PutAsync("/api/tax/999/unlink?itemsAreProducts=true",
            Json("[1]"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- GetLinkedToItem ---

    [Fact]
    public async Task GetLinkedToItem_ValidItem_Returns200()
    {
        var response = await _client.GetAsync("/api/tax/item/1?isProduct=true");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetLinkedToItem_ItemNotFound_Returns404()
    {
        _factory.TaxServiceMock
            .Setup(x => x.GetTaxesLinkedToItemId(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Item not found"));

        var response = await _client.GetAsync("/api/tax/item/999?isProduct=true");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
