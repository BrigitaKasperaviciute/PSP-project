using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using POS_System.Business.Dtos.Request;
using POS_System.Common.Exceptions;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class ServiceControllerTests : IClassFixture<ApiLayerTestApplicationFactory>
{
    private readonly ApiLayerTestApplicationFactory _factory;
    private readonly HttpClient _client;

    public ServiceControllerTests(ApiLayerTestApplicationFactory factory)
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
        "{\"name\":\"Haircut\",\"description\":\"Standard cut\",\"duration\":30,\"price\":1500,\"imageURL\":\"https://example.com/img.png\",\"employeeId\":1}";

    // --- GetById ---

    [Fact]
    public async Task GetById_ExistingService_Returns200()
    {
        var response = await _client.GetAsync("/api/services/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetById_NonExistingService_Returns404()
    {
        _factory.ServiceServiceMock
            .Setup(x => x.GetServiceByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Service not found"));

        var response = await _client.GetAsync("/api/services/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Create ---

    [Fact]
    public async Task Create_ValidRequest_Returns200()
    {
        var response = await _client.PostAsync("/api/services", Json(ValidBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Create_ServiceThrowsBadRequest_Returns400()
    {
        _factory.ServiceServiceMock
            .Setup(x => x.CreateServiceAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Employee not found"));

        var response = await _client.PostAsync("/api/services", Json(ValidBody));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Update ---

    [Fact]
    public async Task Update_ExistingService_Returns200()
    {
        var response = await _client.PutAsync("/api/services/1", Json(ValidBody));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Update_NonExistingService_Returns404()
    {
        _factory.ServiceServiceMock
            .Setup(x => x.UpdateServiceAsync(It.IsAny<int>(), It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Service not found"));

        var response = await _client.PutAsync("/api/services/999", Json(ValidBody));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_ExistingService_ReturnsSuccess()
    {
        var response = await _client.DeleteAsync("/api/services/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Delete_NonExistingService_Returns404()
    {
        _factory.ServiceServiceMock
            .Setup(x => x.DeleteServiceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Service not found"));

        var response = await _client.DeleteAsync("/api/services/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- GetLinkedToTax ---

    [Fact]
    public async Task GetLinkedToTax_ValidTax_Returns200()
    {
        var response = await _client.GetAsync("/api/services/tax/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetLinkedToTax_TaxNotFound_Returns404()
    {
        _factory.ServiceServiceMock
            .Setup(x => x.GetServicesLinkedToTaxId(It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Tax not found"));

        var response = await _client.GetAsync("/api/services/tax/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- GetLinkedToItemDiscount ---

    [Fact]
    public async Task GetLinkedToItemDiscount_ValidDiscount_Returns200()
    {
        var response = await _client.GetAsync("/api/services/item-discount/1");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetLinkedToItemDiscount_DiscountNotFound_Returns404()
    {
        _factory.ServiceServiceMock
            .Setup(x => x.GetServicesLinkedToItemDiscountId(It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Item discount not found"));

        var response = await _client.GetAsync("/api/services/item-discount/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
