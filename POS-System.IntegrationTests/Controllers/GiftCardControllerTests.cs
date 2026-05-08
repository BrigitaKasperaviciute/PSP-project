using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using POS_System.Business.Dtos.Request;
using POS_System.Common.Exceptions;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class GiftCardControllerTests : IClassFixture<ApiLayerTestApplicationFactory>
{
    private readonly ApiLayerTestApplicationFactory _factory;
    private readonly HttpClient _client;

    public GiftCardControllerTests(ApiLayerTestApplicationFactory factory)
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

    private const string ValidBody = "{\"date\":\"2030-01-01\",\"value\":100}";

    // --- GetAll ---

    [Fact]
    public async Task GetAll_DefaultParams_Returns200()
    {
        var response = await _client.GetAsync("/api/giftcards?pageNum=0&pageSize=5");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetAll_ServiceThrowsInternalError_Returns500()
    {
        _factory.GiftCardServiceMock
            .Setup(x => x.GetAllGiftCardsAsync(It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("Storage error"));

        var response = await _client.GetAsync("/api/giftcards?pageNum=0&pageSize=5");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_ExistingGiftCard_Returns200()
    {
        var response = await _client.GetAsync("/api/giftcards/GC-001");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetById_NonExistingGiftCard_Returns404()
    {
        _factory.GiftCardServiceMock
            .Setup(x => x.GetGiftCardByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Gift card not found"));

        var response = await _client.GetAsync("/api/giftcards/NONEXISTENT");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Create ---

    [Fact]
    public async Task Create_ValidRequest_Returns200()
    {
        var response = await _client.PostAsync("/api/giftcards", Json(ValidBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Create_ServiceThrowsConflict_Returns409()
    {
        _factory.GiftCardServiceMock
            .Setup(x => x.CreateGiftCardAsync(It.IsAny<GiftCardRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("Gift card code already exists"));

        var response = await _client.PostAsync("/api/giftcards", Json(ValidBody));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // --- Update ---

    [Fact]
    public async Task Update_ExistingGiftCard_Returns200()
    {
        var response = await _client.PutAsync("/api/giftcards/GC-001",
            Json("{\"date\":\"2031-01-01\",\"value\":200}"));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Update_NonExistingGiftCard_Returns404()
    {
        _factory.GiftCardServiceMock
            .Setup(x => x.UpdateGiftCardAsync(It.IsAny<string>(), It.IsAny<GiftCardRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Gift card not found"));

        var response = await _client.PutAsync("/api/giftcards/NONEXISTENT",
            Json("{\"date\":\"2031-01-01\",\"value\":200}"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_ExistingGiftCard_Returns204()
    {
        var response = await _client.DeleteAsync("/api/giftcards/GC-001");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Delete_NonExistingGiftCard_Returns404()
    {
        _factory.GiftCardServiceMock
            .Setup(x => x.DeleteGiftCardAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Gift card not found"));

        var response = await _client.DeleteAsync("/api/giftcards/NONEXISTENT");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
