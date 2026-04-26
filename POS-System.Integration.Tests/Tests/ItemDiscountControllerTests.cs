using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Response;
using POS_System.Integration.Tests.Helpers;

namespace POS_System.Integration.Tests.Tests;

public class ItemDiscountControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClientWithAllClaims();
    private readonly HttpClient _anonClient = factory.CreateClient();

    [Fact]
    public async Task GetAll_WithValidToken_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/item-discount");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/item-discount");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_ExistingDiscount_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/item-discount/2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_NonExistingDiscount_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/item-discount/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ValidDiscount_ReturnsOk()
    {
        var request = new
        {
            Value = 10,
            IsPercentage = true,
            Description = "Test Discount",
            StartDate = (DateTime?)null,
            EndDate = (DateTime?)null
        };

        var response = await _client.PostAsJsonAsync("/api/item-discount", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_ExistingDiscount_ReturnsOk()
    {
        var createRequest = new
        {
            Value = 5,
            IsPercentage = true,
            Description = "Discount To Update",
            StartDate = (DateTime?)null,
            EndDate = (DateTime?)null
        };
        var createResponse = await _client.PostAsJsonAsync("/api/item-discount", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ItemDiscountResponse>();

        var updateRequest = new
        {
            Value = 8,
            IsPercentage = false,
            Description = "Updated Discount",
            StartDate = (DateTime?)null,
            EndDate = (DateTime?)null
        };
        var response = await _client.PutAsJsonAsync($"/api/item-discount/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_ExistingDiscount_ReturnsOk()
    {
        var createRequest = new
        {
            Value = 3,
            IsPercentage = true,
            Description = "Discount To Delete",
            StartDate = (DateTime?)null,
            EndDate = (DateTime?)null
        };
        var createResponse = await _client.PostAsJsonAsync("/api/item-discount", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ItemDiscountResponse>();

        var response = await _client.DeleteAsync($"/api/item-discount/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkAndUnlink_ProductsToDiscount_ReturnsOk()
    {
        var createRequest = new
        {
            Value = 7,
            IsPercentage = true,
            Description = "Link Discount",
            StartDate = (DateTime?)null,
            EndDate = (DateTime?)null
        };
        var createResponse = await _client.PostAsJsonAsync("/api/item-discount", createRequest);
        var discount = await createResponse.Content.ReadFromJsonAsync<ItemDiscountResponse>();

        var linkResponse = await _client.PutAsJsonAsync(
            $"/api/item-discount/{discount!.Id}/link?itemsAreProducts=true", new[] { 4 });
        linkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var unlinkResponse = await _client.PutAsJsonAsync(
            $"/api/item-discount/{discount.Id}/unlink?itemsAreProducts=true", new[] { 4 });
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDiscountsLinkedToItem_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/item-discount/item/4?isProduct=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
