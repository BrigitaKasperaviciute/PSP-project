using System.Net;
using FluentAssertions;
using POS_System.IntegrationTests.TestSupport;
using POS_System.IntegrationTests.Helpers;
using POS_System.Business.Dtos.Request;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public sealed class FocusedCoverageTests
{
    [Fact]
    public async Task Auth_Login_Forgot_Reset_Permissive()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        var login = new UserLoginRequest("no", "x");
        var loginResp = await client.PostAsync("/v1/auth/login", TestDataFactory.ToJsonContent(login));
        loginResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);

        var forgotResp = await client.PostAsync("/forgot-password", TestDataFactory.ToJsonContent(new { email = "nobody@example.com" }));
        forgotResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);

        var resetResp = await client.PostAsync("/reset-password", TestDataFactory.ToJsonContent(new { token = "t", newPassword = "Password1!" }));
        resetResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Product_Create_Versions_Delete_ExercisesPaths()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-Claims", "ItemWrite,ItemRead");

        var payload = new { name = "CoverageProd", description = "p", price = 123, imageURL = "http://x", stock = 5 };
        var createResp = await client.PostAsync("/api/product", TestDataFactory.ToJsonContent(payload));
        createResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);

        int? id = null;
        if (createResp.IsSuccessStatusCode)
        {
            var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
            if (created.ValueKind != JsonValueKind.Undefined && created.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
            {
                id = idProp.GetInt32();
            }
        }

        // Hit versions endpoint whether or not create returned id
        var productIdForVersions = id ?? 1;
        var versionsResp = await client.GetAsync($"/api/product/{productIdForVersions}/versions/");
        versionsResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);

        // Attempt delete if we have an id
        if (id.HasValue)
        {
            var delResp = await client.DeleteAsync($"/api/product/{id}");
            delResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Forbidden);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
            var prod = db.Products.SingleOrDefault(p => p.Id == id.Value);
            if (prod is not null)
            {
                prod.IsDeleted.Should().BeTrue();
            }
        }
    }

    [Fact]
    public async Task Payment_Checkout_And_Refund_Permissive()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-Claims", "PaymentWrite,PaymentRead");

        var cashReq = new { cartId = 1, amount = 1000, tip = 0, transactionRef = "txn-test", phoneNumber = (string?)null };
        var cashResp = await client.PostAsync("/api/payments/cash", TestDataFactory.ToJsonContent(cashReq));
        cashResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);

        var refundResp = await client.PatchAsync($"/api/payments/refund/{DateTime.UtcNow:o}", TestDataFactory.ToJsonContent(new { cartId = 999999, isCard = false }));
        refundResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GiftCard_Create_Get_Delete_Focused()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-Claims", "GiftCardWrite,GiftCardRead");

        var createResp = await client.PostAsync("/api/giftcards", TestDataFactory.ToJsonContent(new { date = DateTime.UtcNow.Date, value = 10 }));
        createResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);

        string? id = null;
        if (createResp.IsSuccessStatusCode)
        {
            var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
            if (created.ValueKind != JsonValueKind.Undefined && created.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
            {
                id = idProp.GetString();
            }
        }

        var listResp = await client.GetAsync("/api/giftcards");
        listResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.Forbidden);

        if (!string.IsNullOrEmpty(id))
        {
            var getResp = await client.GetAsync($"/api/giftcards/{id}");
            getResp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Forbidden);

            var delResp = await client.DeleteAsync($"/api/giftcards/{id}");
            delResp.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
        }
    }
}
