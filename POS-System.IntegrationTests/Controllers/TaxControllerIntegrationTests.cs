using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class TaxControllerIntegrationTests
{
    [Fact]
    public async Task GetAll_WithReadClaim_ReturnsPagedTaxes()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "TaxRead" });

        var response = await client.GetAsync("/api/tax?pageNum=0&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<TaxResponse>>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetById_WithReadClaim_ReturnsTax()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "TaxRead" });

        var response = await client.GetAsync("/api/tax/4");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(4);
    }

    [Fact]
    public async Task CreateTax_ValidPayload_PersistsTaxAndReturnsResponse()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "TaxRead", "TaxWrite" });

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Taxes.CountAsync());
        var payload = new TaxRequest
        {
            Name = $"VAT-{Guid.NewGuid():N}"[..16],
            Rate = 15,
            IsPercentage = true
        };

        // Act
        var createResponse = await client.PostAsJsonAsync("/api/tax", payload);

        // Assert
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdTax = await createResponse.Content.ReadFromJsonAsync<TaxResponse>();
        createdTax.Should().NotBeNull();
        createdTax!.Name.Should().Be(payload.Name);
        createdTax.Rate.Should().Be(payload.Rate);
        createdTax.IsPercentage.Should().BeTrue();

        var listResponse = await client.GetAsync("/api/tax?pageNum=0&pageSize=25");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Taxes.CountAsync());
        countAfter.Should().Be(countBefore + 1);

        var getResponse = await client.GetAsync($"/api/tax/{createdTax.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedTax = await getResponse.Content.ReadFromJsonAsync<TaxResponse>();
        fetchedTax.Should().NotBeNull();
        fetchedTax!.Name.Should().Be(payload.Name);

        var deleteResponse = await client.DeleteAsync("/api/tax/2");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deletedTax = await factory.ExecuteDbContextAsync(db => db.Taxes.SingleAsync(x => x.Id == 2));
        deletedTax.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTax_ExistingTaxReturnsUpdatedResponseAndPersistsChanges()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "TaxWrite" });

        // Arrange
        var request = new TaxRequest
        {
            Name = "Updated Tax Name",
            Rate = 21,
            IsPercentage = true
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/tax/2", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedTax = await response.Content.ReadFromJsonAsync<TaxResponse>();
        updatedTax.Should().NotBeNull();
        updatedTax!.Name.Should().Be(request.Name);
        updatedTax.Rate.Should().Be(request.Rate);

        var persistedTax = await factory.ExecuteDbContextAsync(db => db.Taxes.SingleAsync(x => x.Id == 2));
        persistedTax.IsDeleted.Should().BeTrue();

        var versionCountAfter = await factory.ExecuteDbContextAsync(db => db.Taxes.CountAsync(x => x.TaxId == 2));
        versionCountAfter.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task LinkAndUnlink_WithWriteClaim_UpdatesJoinRows()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "TaxWrite", "TaxRead" });

        var linkResponse = await client.PutAsJsonAsync("/api/tax/4/link?itemsAreProducts=true", new[] { 4 });
        linkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var linkedResponse = await client.GetAsync("/api/tax/item/4?isProduct=true");
        linkedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var linkedTaxes = await linkedResponse.Content.ReadFromJsonAsync<IEnumerable<TaxResponse>>();
        linkedTaxes.Should().NotBeNull();
        linkedTaxes.Should().NotBeEmpty();

        var unlinkResponse = await client.PutAsJsonAsync("/api/tax/4/unlink?itemsAreProducts=true", new[] { 4 });
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterUnlinkResponse = await client.GetAsync("/api/tax/item/4?isProduct=true");
        afterUnlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterUnlinkTaxes = await afterUnlinkResponse.Content.ReadFromJsonAsync<IEnumerable<TaxResponse>>();
        afterUnlinkTaxes.Should().NotBeNull();
        afterUnlinkTaxes.Should().BeEmpty();
    }
}