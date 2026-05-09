using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.IntegrationTests.TestSupport;
using POS_System.IntegrationTests.Helpers;
using POS_System.Business.Dtos.Request;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class CoverageAdditionalControllerTests
{
    [Fact]
    public async Task GiftCard_FullCrud_CoversGetAllGetByIdUpdateDelete()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        // allow write and read
        client.DefaultRequestHeaders.Add("X-Test-Claims", "GiftCardWrite,GiftCardRead");

        var createReq = new GiftCardRequest { Date = DateOnly.FromDateTime(DateTime.UtcNow), Value = 42 };
        var createResp = await client.PostAsync("api/giftcards", TestDataFactory.ToJsonContent(createReq));
        createResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // get id from DB
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        var created = db.GiftCards.SingleOrDefault(g => g.Value == 42);
        created.Should().NotBeNull();
        var id = created!.Id;

        // GET all
        var allResp = await client.GetAsync("api/giftcards");
        allResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // GET by id
        var getResp = await client.GetAsync($"api/giftcards/{id}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Update
        var updateReq = new GiftCardRequest { Date = createReq.Date, Value = 99 };
        var updateResp = await client.PutAsync($"api/giftcards/{id}", TestDataFactory.ToJsonContent(updateReq));
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Re-query in a fresh scope to avoid stale tracked entity
        using (var scope2 = factory.Services.CreateScope())
        {
            var db2 = scope2.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
            var updated = db2.GiftCards.SingleOrDefault(g => g.Id == id);
            updated!.Value.Should().Be(99);
        }

        // Delete
        var delResp = await client.DeleteAsync($"api/giftcards/{id}");
        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope3 = factory.Services.CreateScope())
        {
            var db3 = scope3.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
            var exists = db3.GiftCards.Any(g => g.Id == id);
            exists.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Product_GetVersions_And_Delete_Flow()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        // Give both read and write
        client.DefaultRequestHeaders.Add("X-Test-Claims", "ItemWrite,ItemRead");

        var name = "CovProd-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var createReq = new ProductRequest
        {
            Name = name,
            Description = "Coverage product",
            Price = 123,
            ImageURL = "http://example.com/p.png",
            Stock = 2
        };

        var createResp = await client.PostAsync("api/product", TestDataFactory.ToJsonContent(createReq));
        createResp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        var prod = db.Products.SingleOrDefault(p => p.Name == name);
        prod.Should().NotBeNull();
        var id = prod!.Id;

        // GET all
        var allResp = await client.GetAsync("api/product");
        allResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // GET by id
        var getResp = await client.GetAsync($"api/product/{id}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // GET versions
        var versResp = await client.GetAsync($"api/product/{id}/versions/");
        versResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Delete
        var delResp = await client.DeleteAsync($"api/product/{id}");
        delResp.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope2 = factory.Services.CreateScope())
        {
            var db2 = scope2.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
            var softDeleted = db2.Products.SingleOrDefault(p => p.Id == id);
            softDeleted.Should().NotBeNull();
            softDeleted!.IsDeleted.Should().BeTrue();
        }
    }
}
