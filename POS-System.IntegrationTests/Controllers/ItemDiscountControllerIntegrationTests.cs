using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class ItemDiscountControllerIntegrationTests
{
    [Fact]
    public async Task GetAll_WithReadClaim_ReturnsPagedItemDiscounts()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemDiscountRead" });

        // Arrange - create an active discount so the endpoint has a stable result.
        await factory.ExecuteDbContextAsync(async db =>
        {
            db.ItemDiscounts.Add(new POS_System.Domain.Entities.ItemDiscount
            {
                ItemDiscountId = 99,
                Value = 20,
                IsPercentage = true,
                Description = "Active discount for get-all",
                StartDate = null,
                EndDate = null,
                Version = DateTime.UtcNow,
                IsDeleted = false
            });
            await db.SaveChangesAsync();
            return 0;
        });

        // Act
        var response = await client.GetAsync("/api/item-discount?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ItemDiscountResponse>>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().BeGreaterThan(0);
        body.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetById_WithReadClaim_ReturnsItemDiscount()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemDiscountRead" });

        // Act
        var response = await client.GetAsync("/api/item-discount/2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(2);
    }

    [Fact]
    public async Task Create_WithWriteClaim_PersistsItemDiscount()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemDiscountWrite" });

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.ItemDiscounts.CountAsync());
        var payload = new ItemDiscountRequest
        {
            Value = 25,
            IsPercentage = true,
            Description = "Integration discount",
            StartDate = null,
            EndDate = null
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/item-discount", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(payload.Value);
        body.IsPercentage.Should().BeTrue();

        var countAfter = await factory.ExecuteDbContextAsync(db => db.ItemDiscounts.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task Update_WithWriteClaim_VersionsItemDiscount()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemDiscountWrite" });

        // Arrange - create a fresh discount so versioning is easy to verify.
        var created = await (await client.PostAsJsonAsync("/api/item-discount", new ItemDiscountRequest
        {
            Value = 12,
            IsPercentage = true,
            Description = "Version me",
            StartDate = null,
            EndDate = null
        })).Content.ReadFromJsonAsync<ItemDiscountResponse>();

        created.Should().NotBeNull();
        var countBefore = await factory.ExecuteDbContextAsync(db => db.ItemDiscounts.CountAsync());

        var updatePayload = new ItemDiscountRequest
        {
            Value = 40,
            IsPercentage = false,
            Description = "Updated discount",
            StartDate = null,
            EndDate = null
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/item-discount/{created!.Id}", updatePayload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(40);
        body.IsPercentage.Should().BeFalse();

        var countAfter = await factory.ExecuteDbContextAsync(db => db.ItemDiscounts.CountAsync());
        countAfter.Should().Be(countBefore + 1);

        var oldRecord = await factory.ExecuteDbContextAsync(db => db.ItemDiscounts.SingleAsync(discount => discount.Id == created.Id));
        oldRecord.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithWriteClaim_MarksItemDiscountDeleted()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemDiscountWrite" });

        // Arrange - create a discount so we don't delete a seeded row.
        var created = await (await client.PostAsJsonAsync("/api/item-discount", new ItemDiscountRequest
        {
            Value = 15,
            IsPercentage = false,
            Description = "Delete me",
            StartDate = null,
            EndDate = null
        })).Content.ReadFromJsonAsync<ItemDiscountResponse>();

        created.Should().NotBeNull();

        // Act
        var response = await client.DeleteAsync($"/api/item-discount/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var persisted = await factory.ExecuteDbContextAsync(db => db.ItemDiscounts.SingleAsync(discount => discount.Id == created.Id));
        persisted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task LinkAndUnlink_WithWriteClaim_UpdatesJoinRows()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemDiscountWrite", "ItemDiscountRead" });

        // Arrange - create a fresh discount for linking.
        var created = await (await client.PostAsJsonAsync("/api/item-discount", new ItemDiscountRequest
        {
            Value = 18,
            IsPercentage = true,
            Description = "Link me",
            StartDate = null,
            EndDate = null
        })).Content.ReadFromJsonAsync<ItemDiscountResponse>();

        created.Should().NotBeNull();
        var itemIdList = new[] { 4 };

        // Act - link to seeded product 4.
        var linkResponse = await client.PutAsJsonAsync($"/api/item-discount/{created!.Id}/link?itemsAreProducts=true", itemIdList);

        // Assert - link
        linkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var linkedCount = await factory.ExecuteDbContextAsync(db => db.ProductOnItemDiscounts.CountAsync(x => x.RightEntityId == created.Id && x.EndDate == null));
        linkedCount.Should().Be(1);

        // Act - fetch linked discounts by product.
        var getLinkedResponse = await client.GetAsync("/api/item-discount/item/4?isProduct=true");
        getLinkedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var linkedDiscounts = await getLinkedResponse.Content.ReadFromJsonAsync<List<ItemDiscountResponse>>();
        linkedDiscounts.Should().NotBeNull();
        linkedDiscounts!.Should().ContainSingle(discount => discount.Id == created.Id);

        // Act - unlink.
        var unlinkResponse = await client.PutAsJsonAsync($"/api/item-discount/{created.Id}/unlink?itemsAreProducts=true", itemIdList);

        // Assert - unlink
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeCountAfterUnlink = await factory.ExecuteDbContextAsync(db => db.ProductOnItemDiscounts.CountAsync(x => x.RightEntityId == created.Id && x.EndDate == null));
        activeCountAfterUnlink.Should().Be(0);
    }
}
