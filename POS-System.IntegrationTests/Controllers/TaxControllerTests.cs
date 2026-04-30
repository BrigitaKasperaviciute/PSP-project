using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Domain.Entities;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class TaxControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TaxControllerTests(PosSystemApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Taxes.RemoveRange(db.Taxes.ToList());
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── GET ALL ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllTaxes_WithValidAuth_ReturnsOkWithTaxList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Taxes.Add(new Tax { Id = 1001, TaxId = 1001, Name = "VAT", Rate = 21, IsPercentage = true, Version = DateTime.UtcNow, IsDeleted = false });
        db.Taxes.Add(new Tax { Id = 1002, TaxId = 1002, Name = "Excise", Rate = 500, IsPercentage = false, Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("TaxRead");

        // Act
        var response = await client.GetAsync("/api/tax");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<TaxResponse>>(_jsonOptions);
        body!.TotalCount.Should().Be(2);
        body.Results.Should().Contain(t => t.Name == "VAT");
    }

    [Fact]
    public async Task GetAllTaxes_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/tax");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET BY ID ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTaxById_WithExistingId_ReturnsOkWithTax()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Taxes.Add(new Tax { Id = 1003, TaxId = 1003, Name = "GST", Rate = 10, IsPercentage = true, Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("TaxRead");

        // Act
        var response = await client.GetAsync("/api/tax/1003");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>(_jsonOptions);
        body!.Name.Should().Be("GST");
        body.Rate.Should().Be(10);
        body.IsPercentage.Should().BeTrue();
    }

    [Fact]
    public async Task GetTaxById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("TaxRead");

        // Act
        var response = await client.GetAsync("/api/tax/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── CREATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTax_WithValidRequest_ReturnsOkWithCreatedTax()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("TaxWrite");
        var request = new TaxRequest { Name = "NewTax", Rate = 15, IsPercentage = true };

        // Act
        var response = await client.PostAsJsonAsync("/api/tax", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>(_jsonOptions);
        body!.Name.Should().Be("NewTax");
        body.Rate.Should().Be(15);
        body.IsPercentage.Should().BeTrue();
        body.Id.Should().BeGreaterThan(0);

        // Verify database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Taxes.FindAsync(body.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("NewTax");
        saved.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTax_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new TaxRequest { Name = "Tax", Rate = 5, IsPercentage = true };

        // Act
        var response = await client.PostAsJsonAsync("/api/tax", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── UPDATE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTax_WithExistingId_ReturnsOkWithUpdatedTax()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Taxes.Add(new Tax { Id = 1004, TaxId = 1004, Name = "OldName", Rate = 5, IsPercentage = true, Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("TaxWrite");
        var updateRequest = new TaxRequest { Name = "UpdatedName", Rate = 20, IsPercentage = false };

        // Act
        var response = await client.PutAsJsonAsync("/api/tax/1004", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>(_jsonOptions);
        body!.Name.Should().Be("UpdatedName");
        body.Rate.Should().Be(20);
        body.IsPercentage.Should().BeFalse();

        // Verify old record marked deleted in database
        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var old = await assertDb.Taxes.FindAsync(1004);
        old!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTax_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("TaxWrite");
        var request = new TaxRequest { Name = "X", Rate = 1, IsPercentage = true };

        // Act
        var response = await client.PutAsJsonAsync("/api/tax/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteTax_WithExistingId_ReturnsOkAndMarksTaxDeleted()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Taxes.Add(new Tax { Id = 1005, TaxId = 1005, Name = "ToDelete", Rate = 3, IsPercentage = true, Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("TaxWrite");

        // Act
        var response = await client.DeleteAsync("/api/tax/1005");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify database state: tax is soft-deleted
        using var assertScope = _factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await assertDb.Taxes.FindAsync(1005);
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteTax_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("TaxWrite");

        // Act
        var response = await client.DeleteAsync("/api/tax/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── LINK / UNLINK ────────────────────────────────────────────────────────

    [Fact]
    public async Task LinkTaxToProducts_WithValidIds_ReturnsOk()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Taxes.Add(new Tax { Id = 1006, TaxId = 1006, Name = "LinkTax", Rate = 7, IsPercentage = true, Version = DateTime.UtcNow, IsDeleted = false });
        db.Products.Add(new Product { Id = 1006, ProductId = 1006, Name = "Prod", Description = "d", Price = 100, Stock = 5, ImageURL = "", Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("TaxWrite");

        // Act
        var response = await client.PutAsJsonAsync("/api/tax/1006/link?itemsAreProducts=true", new[] { 1006 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkTaxToProducts_WithNonExistentTaxId_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("TaxWrite");

        // Act
        var response = await client.PutAsJsonAsync("/api/tax/99999/link?itemsAreProducts=true", new[] { 1 });

        // Assert
        // ManyToManyService.LinkItemToItemsAsync silently no-ops when the tax entity is not found
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnlinkTaxFromItems_WithValidAuth_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClientWithClaims("TaxWrite");

        // Act — ManyToManyService is a silent no-op when entities don't exist
        var response = await client.PutAsJsonAsync("/api/tax/99999/unlink?itemsAreProducts=true", new[] { 1 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── GET TAXES LINKED TO ITEM ─────────────────────────────────────────────

    [Fact]
    public async Task GetTaxesLinkedToItem_WithValidProductId_ReturnsOkWithEmptyList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Products.Add(new Product { Id = 1007, ProductId = 1007, Name = "P", Description = "d", Price = 50, Stock = 1, ImageURL = "", Version = DateTime.UtcNow, IsDeleted = false });
        await db.SaveChangesAsync();

        var client = _factory.CreateClientWithClaims("TaxRead");

        // Act
        var response = await client.GetAsync("/api/tax/item/1007?isProduct=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<TaxResponse>>(_jsonOptions);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTaxesLinkedToItem_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/tax/item/1?isProduct=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
