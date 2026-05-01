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

public class TaxControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly HttpClient _readClient;
    private readonly HttpClient _writeClient;
    private readonly HttpClient _anonClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TaxControllerTests(PosSystemApiFactory factory)
    {
        _factory = factory;
        _readClient  = factory.CreateClientWithClaims("TaxRead");
        _writeClient = factory.CreateClientWithClaims("TaxRead", "TaxWrite");
        _anonClient  = factory.CreateAnonymousClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── GET /api/tax ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WithTaxReadClaim_ReturnsOkWithPagedResult()
    {
        // Arrange — 4 tax rows seeded (2 active, 2 deleted)

        // Act
        var response = await _readClient.GetAsync("/api/tax");
        var body = await response.Content.ReadAsStringAsync();
        var paged = JsonSerializer.Deserialize<PagedResponse<TaxResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paged.Should().NotBeNull();
        paged!.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonClient.GetAsync("/api/tax");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/tax/{id} ────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_WithExistingId_ReturnsOkWithTax()
    {
        // Arrange — tax Id=2 is active (TaxId=2, Name="Tax2", Rate=10, IsPercentage=true)

        // Act
        var response = await _readClient.GetAsync("/api/tax/2");
        var body = await response.Content.ReadAsStringAsync();
        var tax = JsonSerializer.Deserialize<TaxResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        tax.Should().NotBeNull();
        tax!.Id.Should().Be(2);
        tax.Name.Should().Be("Tax2");
        tax.Rate.Should().Be(10);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _readClient.GetAsync("/api/tax/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/tax ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidData_ReturnsOkAndPersistsTax()
    {
        // Arrange
        var request = new TaxRequest
        {
            Name         = "Integration VAT",
            Rate         = 21,
            IsPercentage = true
        };

        // Act
        var response = await _writeClient.PostAsJsonAsync("/api/tax", request);
        var body = await response.Content.ReadAsStringAsync();
        var tax = JsonSerializer.Deserialize<TaxResponse>(body, JsonOptions);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        tax.Should().NotBeNull();
        tax!.Name.Should().Be("Integration VAT");
        tax.Rate.Should().Be(21);

        // Assert – database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.Taxes.FindAsync(tax.Id);
        persisted.Should().NotBeNull();
        persisted!.IsPercentage.Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithoutTaxWriteClaim_ReturnsUnauthorized()
    {
        // Arrange — read-only client lacks TaxWrite
        var request = new TaxRequest { Name = "Blocked Tax", Rate = 5, IsPercentage = true };

        // Act
        var response = await _readClient.PostAsJsonAsync("/api/tax", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PUT /api/tax/{id} ─────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WithExistingId_ReturnsOkWithUpdatedTax()
    {
        // Arrange
        var request = new TaxRequest { Name = "Updated Tax2", Rate = 15, IsPercentage = true };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/tax/2", request);
        var body = await response.Content.ReadAsStringAsync();
        var tax = JsonSerializer.Deserialize<TaxResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        tax!.Name.Should().Be("Updated Tax2");
        tax.Rate.Should().Be(15);
    }

    [Fact]
    public async Task Update_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new TaxRequest { Name = "Ghost", Rate = 1, IsPercentage = false };

        // Act
        var response = await _writeClient.PutAsJsonAsync("/api/tax/9999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/tax/{id} ─────────────────────────────────────────────────

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOkAndSoftDeletesTax()
    {
        // Arrange — tax Id=2 is active

        // Act
        var response = await _writeClient.DeleteAsync("/api/tax/2");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – soft-deleted in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var latest = await db.Taxes
            .Where(t => t.TaxId == 2)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync();
        latest!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _writeClient.DeleteAsync("/api/tax/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT /api/tax/{id}/link & /unlink ────────────────────────────────────

    [Fact]
    public async Task LinkTaxToProducts_WithValidIds_ReturnsOkAndCreatesLink()
    {
        // Arrange — link active tax (Id=2) to active product (Id=4)
        var productIds = new[] { 4 };

        // Act
        var response = await _writeClient.PutAsJsonAsync(
            "/api/tax/2/link?itemsAreProducts=true", productIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – many-to-many row in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var link = db.ProductOnTaxes.FirstOrDefault(p => p.RightEntityId == 2 && p.LeftEntityId == 4);
        link.Should().NotBeNull();
    }

    [Fact]
    public async Task UnlinkTaxFromProducts_WithNonExistentLink_ReturnsOk()
    {
        // Arrange — no link exists yet between tax 2 and product 4

        // Act — unlink should be idempotent / no error
        var response = await _writeClient.PutAsJsonAsync(
            "/api/tax/2/unlink?itemsAreProducts=true", new[] { 4 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
