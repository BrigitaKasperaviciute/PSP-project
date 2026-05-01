using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.IntegrationTests;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

/// <summary>
/// Seeded item discounts:
///   Id=1  ItemDiscountId=1  Value=12  IsDeleted=true
///   Id=2  ItemDiscountId=2  Value=15  IsDeleted=false
///   Id=3  ItemDiscountId=3  Value=500 IsDeleted=false
///   Id=4  ItemDiscountId=1  Value=18  IsDeleted=true
/// </summary>
public class ItemDiscountControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly HttpClient _readClient;
    private readonly HttpClient _writeClient;
    private readonly HttpClient _anonClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ItemDiscountControllerTests(PosSystemApiFactory factory)
    {
        _factory = factory;
        _readClient  = factory.CreateClientWithClaims("ItemDiscountRead");
        _writeClient = factory.CreateClientWithClaims("ItemDiscountRead", "ItemDiscountWrite");
        _anonClient  = factory.CreateAnonymousClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Helper
    private async Task<ItemDiscountResponse> CreateItemDiscountAsync(int value = 10)
    {
        var request = new ItemDiscountRequest
        {
            Value        = value,
            IsPercentage = true,
            Description  = "Test discount",
            StartDate    = null,
            EndDate      = null
        };
        var response = await _writeClient.PostAsJsonAsync("/api/item-discount", request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ItemDiscountResponse>(body, JsonOptions)!;
    }

    // ── GET /api/item-discount ────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WithItemDiscountReadClaim_ReturnsOkWithPagedResult()
    {
        // Arrange — seeded discounts are expired; create a fresh non-expiring one
        await CreateItemDiscountAsync();

        // Act
        var response = await _readClient.GetAsync("/api/item-discount");
        var body = await response.Content.ReadAsStringAsync();
        var paged = JsonSerializer.Deserialize<PagedResponse<ItemDiscountResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paged.Should().NotBeNull();
        paged!.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/item-discount");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/item-discount/{id} ───────────────────────────────────────────

    [Fact]
    public async Task GetById_WithExistingId_ReturnsOkWithItemDiscount()
    {
        // Arrange — discount Id=2 is active (IsDeleted=false)

        // Act
        var response = await _readClient.GetAsync("/api/item-discount/2");
        var body = await response.Content.ReadAsStringAsync();
        var discount = JsonSerializer.Deserialize<ItemDiscountResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        discount.Should().NotBeNull();
        discount!.Id.Should().Be(2);
        discount.Value.Should().Be(15);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _readClient.GetAsync("/api/item-discount/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/item-discount ───────────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidData_ReturnsOkAndPersistsDiscount()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(1);
        var endDate   = DateTime.UtcNow.AddMonths(3);
        var request = new ItemDiscountRequest
        {
            Value        = 25,
            IsPercentage = true,
            Description  = "Summer sale 25%",
            StartDate    = startDate,
            EndDate      = endDate
        };

        // Act
        var response = await _writeClient.PostAsJsonAsync("/api/item-discount", request);
        var body = await response.Content.ReadAsStringAsync();
        var discount = JsonSerializer.Deserialize<ItemDiscountResponse>(body, JsonOptions);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        discount.Should().NotBeNull();
        discount!.Value.Should().Be(25);
        discount.IsPercentage.Should().BeTrue();

        // Assert – database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.ItemDiscounts.FindAsync(discount.Id);
        persisted.Should().NotBeNull();
        persisted!.Description.Should().Be("Summer sale 25%");
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var request = new ItemDiscountRequest
        {
            Value = 5, IsPercentage = false, Description = "Test", StartDate = null, EndDate = null
        };

        // Act
        var response = await _anonClient.PostAsJsonAsync("/api/item-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PUT /api/item-discount/{id} ───────────────────────────────────────────

    [Fact]
    public async Task Update_WithExistingId_ReturnsOkWithUpdatedDiscount()
    {
        // Arrange
        var request = new ItemDiscountRequest
        {
            Value        = 20,
            IsPercentage = true,
            Description  = "Updated discount",
            StartDate    = null,
            EndDate      = null
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/item-discount/2", request);
        var body = await response.Content.ReadAsStringAsync();
        var discount = JsonSerializer.Deserialize<ItemDiscountResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        discount!.Value.Should().Be(20);
        discount.Description.Should().Be("Updated discount");

        // Assert – newest version in database reflects the update
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var latest = await db.ItemDiscounts
            .Where(d => d.ItemDiscountId == 2)
            .OrderByDescending(d => d.Version)
            .FirstOrDefaultAsync();
        latest!.Value.Should().Be(20);
    }

    [Fact]
    public async Task Update_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new ItemDiscountRequest
        {
            Value = 1, IsPercentage = false, Description = "Ghost", StartDate = null, EndDate = null
        };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/item-discount/9999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/item-discount/{id} ────────────────────────────────────────

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOkAndSoftDeletesDiscount()
    {
        // Arrange — create a fresh discount, then delete it
        var created = await CreateItemDiscountAsync(30);

        // Act
        var response = await _writeClient.DeleteAsync($"/api/item-discount/{created.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – soft-deleted in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.ItemDiscounts.FindAsync(created.Id);
        persisted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _writeClient.DeleteAsync("/api/item-discount/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT /api/item-discount/{id}/link & /unlink ────────────────────────────

    [Fact]
    public async Task LinkDiscountToProducts_WithValidIds_ReturnsOkAndCreatesLink()
    {
        // Arrange — link active discount (Id=2) to active product (Id=4)
        var productIds = new[] { 4 };

        // Act
        var response = await _writeClient.PutAsJsonAsync(
            "/api/item-discount/2/link?itemsAreProducts=true", productIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – many-to-many row created
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var link = db.ProductOnItemDiscounts
            .FirstOrDefault(p => p.RightEntityId == 2 && p.LeftEntityId == 4);
        link.Should().NotBeNull();
    }

    [Fact]
    public async Task UnlinkDiscountFromProducts_AfterLink_ReturnsOkAndRemovesLink()
    {
        // Arrange — link first
        await _writeClient.PutAsJsonAsync(
            "/api/item-discount/2/link?itemsAreProducts=true", new[] { 4 });

        // Act — then unlink
        var response = await _writeClient.PutAsJsonAsync(
            "/api/item-discount/2/unlink?itemsAreProducts=true", new[] { 4 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – link is soft-deleted (EndDate set)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var link = db.ProductOnItemDiscounts
            .FirstOrDefault(p => p.RightEntityId == 2 && p.LeftEntityId == 4);
        link.Should().NotBeNull();
        link!.EndDate.Should().NotBeNull();
    }
}
