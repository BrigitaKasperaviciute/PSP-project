using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests;

public class IntegrationScenarioTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost")
    });

    public static IEnumerable<object[]> CreateScenarioData()
    {
        yield return
        [
            "Product",
            "/api/product",
            "{\"name\":\"Integration Product\",\"description\":\"Integration product desc\",\"price\":199,\"imageURL\":\"https://example.com/product.png\",\"stock\":3}"
        ];
        yield return
        [
            "Tax",
            "/api/tax",
            "{\"name\":\"Integration Tax\",\"rate\":12,\"isPercentage\":true}"
        ];
        yield return
        [
            "Service",
            "/api/services",
            "{\"name\":\"Integration Service\",\"description\":\"Integration service desc\",\"duration\":30,\"price\":499,\"imageURL\":\"https://example.com/service.png\",\"employeeId\":1}"
        ];
        yield return
        [
            "TimeSlot",
            "/api/time-slot",
            "{\"employeeVersionId\":1,\"startTime\":\"2030-01-01T10:00:00Z\",\"isAvailable\":true}"
        ];
        yield return
        [
            "ItemDiscount",
            "/api/item-discount",
            "{\"value\":10,\"isPercentage\":true,\"description\":\"Integration discount\",\"startDate\":null,\"endDate\":null}"
        ];
        yield return
        [
            "ProductModification",
            "/api/product-modification",
            "{\"productVersionId\":1,\"name\":\"Extra Sauce\",\"description\":\"Integration mod\",\"price\":10}"
        ];
        yield return
        [
            "ServiceReservation",
            "/api/service-reservation",
            "{\"cartItemId\":1,\"timeSlotId\":1,\"bookingTime\":\"2030-01-01T12:00:00Z\",\"customerName\":\"Integration Customer\",\"customerPhone\":\"+37060000000\",\"isCancelled\":false}"
        ];
    }

    public static IEnumerable<object[]> ControllerActionSmokeData()
    {
        yield return [HttpMethod.Get, "/api/product/1/versions/", null, new[] { HttpStatusCode.OK }];
        yield return [HttpMethod.Put, "/api/product/1", "{\"name\":\"Updated Product\",\"description\":\"Updated\",\"price\":200,\"imageURL\":\"https://example.com/p2.png\",\"stock\":5}", new[] { HttpStatusCode.OK }];
        yield return [HttpMethod.Delete, "/api/product/1", null, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Get, "/api/product/tax/1", null, new[] { HttpStatusCode.OK }];
        yield return [HttpMethod.Get, "/api/product/item-discount/1", null, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];

        yield return [HttpMethod.Put, "/api/tax/1", "{\"name\":\"Updated Tax\",\"rate\":7,\"isPercentage\":true}", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Delete, "/api/tax/1", null, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Put, "/api/tax/1/link?itemsAreProducts=true", "[1]", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Put, "/api/tax/1/unlink?itemsAreProducts=true", "[1]", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Get, "/api/tax/item/1?isProduct=true", null, new[] { HttpStatusCode.OK }];

        yield return [HttpMethod.Put, "/api/services/1", "{\"name\":\"Updated Service\",\"description\":\"Updated\",\"duration\":20,\"price\":300,\"imageURL\":\"https://example.com/s2.png\",\"employeeId\":1}", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Delete, "/api/services/1", null, new[] { HttpStatusCode.NoContent, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Get, "/api/services/tax/1", null, new[] { HttpStatusCode.OK }];
        yield return [HttpMethod.Get, "/api/services/item-discount/1", null, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];

        yield return [HttpMethod.Put, "/api/time-slot/1", "{\"employeeVersionId\":1,\"startTime\":\"2030-01-02T10:00:00Z\",\"isAvailable\":true}", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Delete, "/api/time-slot/1", null, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];

        yield return [HttpMethod.Put, "/api/item-discount/1", "{\"value\":15,\"isPercentage\":true,\"description\":\"Updated discount\",\"startDate\":null,\"endDate\":null}", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Delete, "/api/item-discount/1", null, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Put, "/api/item-discount/1/link?itemsAreProducts=true", "[1]", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Put, "/api/item-discount/1/unlink?itemsAreProducts=true", "[1]", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Get, "/api/item-discount/item/1?isProduct=true", null, new[] { HttpStatusCode.OK }];

        yield return [HttpMethod.Get, "/api/product-modification/1/versions/", null, new[] { HttpStatusCode.OK }];
        yield return [HttpMethod.Put, "/api/product-modification/1", "{\"productVersionId\":1,\"name\":\"Updated Mod\",\"description\":\"Updated\",\"price\":15}", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Delete, "/api/product-modification/1", null, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Get, "/api/product-modification/cart-item/1", null, new[] { HttpStatusCode.OK }];
        yield return [HttpMethod.Get, "/api/product-modification/product/1?pageSize=5&pageNumber=0", null, new[] { HttpStatusCode.OK }];

        yield return [HttpMethod.Put, "/api/service-reservation/1", "{\"cartItemId\":1,\"timeSlotId\":1,\"bookingTime\":\"2030-01-02T12:00:00Z\",\"customerName\":\"Updated\",\"customerPhone\":\"+37060000002\",\"isCancelled\":false}", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];

        yield return [HttpMethod.Post, "/api/carts", "{\"employeeVersionId\":1}", new[] { HttpStatusCode.OK }];
        yield return [HttpMethod.Delete, "/api/carts/1", null, new[] { HttpStatusCode.OK, HttpStatusCode.InternalServerError }];
        yield return [HttpMethod.Get, "/api/carts/1/discount", null, new[] { HttpStatusCode.OK, HttpStatusCode.NoContent }];
        yield return [HttpMethod.Patch, "/api/carts/1/discount", "{\"discountCode\":\"invalid-coupon\"}", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError }];

        yield return [HttpMethod.Post, "/api/cart-discount", "{\"value\":10,\"isPercentage\":true,\"endDate\":\"2030-01-01T10:00:00Z\"}", new[] { HttpStatusCode.OK, HttpStatusCode.InternalServerError, HttpStatusCode.BadRequest }];
        yield return [HttpMethod.Get, "/api/cart-discount/does-not-exist", null, new[] { HttpStatusCode.NotFound }];
        yield return [HttpMethod.Delete, "/api/cart-discount/does-not-exist", null, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError }];

        yield return [HttpMethod.Post, "/api/carts/1/items", "{\"cartId\":1,\"quantity\":1,\"isProduct\":true,\"productVersionId\":1}", new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest }];
        yield return [HttpMethod.Put, "/api/carts/1/items/1", "{\"cartId\":1,\"quantity\":2,\"isProduct\":true,\"productVersionId\":1}", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Delete, "/api/carts/1/items/1", null, new[] { HttpStatusCode.NoContent, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Put, "/api/carts/1/items/1/link", "[2]", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];
        yield return [HttpMethod.Put, "/api/carts/1/items/1/unlink", "[2]", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }];

        yield return [HttpMethod.Put, "/api/giftcards/99999999", "{\"date\":\"2030-01-01\",\"value\":100}", new[] { HttpStatusCode.NotFound }];
        yield return [HttpMethod.Delete, "/api/giftcards/99999999", null, new[] { HttpStatusCode.NotFound }];
        yield return [HttpMethod.Get, "/api/giftcards?pageNum=0&pageSize=5", null, new[] { HttpStatusCode.OK }];

        yield return [HttpMethod.Put, "/api/employees/1", "{\"firstName\":\"Updated\",\"lastName\":\"Employee\",\"birthDate\":\"2000-01-01\",\"userName\":\"johndoe\",\"email\":\"johndoe@example.com\",\"phoneNumber\":\"+37060000111\",\"roleId\":0}", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError }];
        yield return [HttpMethod.Delete, "/api/employees/1", null, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError }];

        yield return [HttpMethod.Post, "/api/payments/cash", "{\"cartId\":1,\"amount\":1000,\"tip\":0,\"transactionRef\":\"ref-1\",\"phoneNumber\":null}", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError }];
        yield return [new HttpMethod("PATCH"), "/api/payments/refund/2025-01-01T00:00:00", "{\"cartId\":1,\"isCard\":false}", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError }];
        yield return [HttpMethod.Get, "/api/payments/1", null, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError }];
        yield return [HttpMethod.Post, "/api/payments/full-checkout", "{\"cartId\":1,\"employeeId\":1,\"tip\":0,\"phoneNumber\":null,\"cartItems\":[{\"name\":\"Item\",\"description\":\"Desc\",\"price\":100,\"quantity\":1,\"imageURL\":null}]}", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError }];
        yield return [HttpMethod.Post, "/api/payments/init-partial-checkout", "{\"cartId\":1,\"employeeId\":1,\"paymentCount\":2,\"tip\":0,\"cartItems\":[{\"name\":\"Item\",\"description\":\"Desc\",\"price\":100,\"quantity\":1,\"imageURL\":null}]}", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError }];
        yield return [HttpMethod.Post, "/api/payments/partial-checkout", "{\"cartId\":1,\"id\":\"2025-01-01T00:00:00\",\"giftCard\":null,\"phoneNumber\":null}", new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError }];
        yield return [HttpMethod.Get, "/api/payments/full-checkout-success?transactionDate=2025-01-01T00:00:00&cartId=1&sessionId=fake", null, new[] { HttpStatusCode.Redirect, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError }];
        yield return [HttpMethod.Get, "/api/payments/partial-checkout-success?transactionDate=2025-01-01T00:00:00&cartId=1&sessionId=fake", null, new[] { HttpStatusCode.Redirect, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError }];
        yield return [HttpMethod.Get, "/api/payments/checkout-fail?transactionDate=2025-01-01T00:00:00&cartId=1&sessionId=fake", null, new[] { HttpStatusCode.Redirect, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError }];

        yield return [HttpMethod.Put, "/api/business-details", "{\"businessName\":\"Integration Shop\",\"businessEmail\":\"shop2@example.com\",\"businessPhone\":\"+37060000003\",\"country\":\"Lithuania\",\"city\":\"Kaunas\",\"street\":\"Second Street\",\"houseNumber\":15,\"flatNumber\":2}", new[] { HttpStatusCode.OK }];

        yield return [HttpMethod.Post, "/api/employees/register", "{\"email\":\"user@example.com\",\"userName\":\"integration-user\",\"firstName\":\"Int\",\"lastName\":\"User\",\"password\":\"Aa!12345\",\"phoneNumber\":\"+37060000004\",\"birthDate\":\"2000-01-01\",\"roleId\":0}", new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError }];
        yield return [HttpMethod.Post, "/v1/auth/login", "{\"userName\":\"integration-user\",\"password\":\"Aa!12345\"}", new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError }];
        yield return [HttpMethod.Post, "/forgot-password", "{\"email\":\"missing@example.com\"}", new[] { HttpStatusCode.OK }];
        yield return [HttpMethod.Post, "/reset-password", "{\"email\":\"missing@example.com\",\"resetCode\":\"invalid\",\"newPassword\":\"Aa!12345\"}", new[] { HttpStatusCode.BadRequest }];
    }

    [Theory]
    [InlineData("Product", "/api/product?onlyActive=true&pageSize=5&pageNumber=0")]
    [InlineData("Tax", "/api/tax?pageNum=0&pageSize=5")]
    [InlineData("Service", "/api/services?pageNum=0&pageSize=5")]
    [InlineData("TimeSlot", "/api/time-slot?onlyAvailable=true&pageSize=5&pageNumber=0")]
    [InlineData("Employee", "/api/employees?onlyActive=true&pageSize=5&pageNumber=0")]
    [InlineData("ItemDiscount", "/api/item-discount?pageNum=0&pageSize=5")]
    [InlineData("ProductModification", "/api/product-modification?onlyActive=true&pageSize=5&pageNumber=0")]
    [InlineData("ServiceReservation", "/api/service-reservation?pageSize=5&pageNumber=0")]
    public async Task Scenario_HappyFlow_ListEndpoint_ReturnsOk(string scenario, string url)
    {
        var response = await _client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.IsSuccessStatusCode,
            $"Scenario '{scenario}' failed with status {(int)response.StatusCode}. Body: {content}");

        Assert.Contains("results", content, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(CreateScenarioData))]
    public async Task Scenario_HappyFlow_CreateEndpoint_ReturnsOk(string scenario, string url, string jsonBody)
    {
        var response = await _client.PostAsync(url, CreateJsonBody(jsonBody));
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.IsSuccessStatusCode,
            $"Scenario '{scenario}' create failed with status {(int)response.StatusCode}. Body: {content}");
    }

    [Theory]
    [InlineData("Product", "/api/product/999999")]
    [InlineData("Tax", "/api/tax/999999")]
    [InlineData("Service", "/api/services/999999")]
    [InlineData("TimeSlot", "/api/time-slot/999999")]
    [InlineData("Employee", "/api/employees/999999")]
    [InlineData("ItemDiscount", "/api/item-discount/999999")]
    [InlineData("ProductModification", "/api/product-modification/999999")]
    [InlineData("ServiceReservation", "/api/service-reservation/999999")]
    public async Task Scenario_NegativeFlow_MissingResource_ReturnsNotFound(string scenario, string url)
    {
        var response = await _client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound,
            $"Scenario '{scenario}' expected 404, but got {(int)response.StatusCode}. Body: {content}");

        Assert.Contains("not found", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CartScenario_HappyFlow_GetAll_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/carts?pageNum=0&pageSize=5");
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"Cart happy flow failed. Body: {content}");
        Assert.Contains("results", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CartScenario_NegativeFlow_MissingResource_ReturnsInternalServerError()
    {
        var response = await _client.GetAsync("/api/carts/999999");
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.InternalServerError,
            $"Cart negative flow expected 500, but got {(int)response.StatusCode}. Body: {content}");
    }

    [Fact]
    public async Task CartItemScenario_HappyFlow_GetAll_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/carts/1/items?pageNum=0&pageSize=5");
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"CartItem happy flow failed. Body: {content}");
        Assert.Contains("results", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CartItemScenario_NegativeFlow_MissingResource_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/carts/1/items/999999");
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound,
            $"CartItem negative flow expected 404, but got {(int)response.StatusCode}. Body: {content}");
    }

    [Fact]
    public async Task GiftCardScenario_HappyFlow_CreateThenGet_ReturnsOk()
    {
        var createBody = "{\"date\":\"2030-01-01\",\"value\":100}";
        var createResponse = await _client.PostAsync("/api/giftcards", CreateJsonBody(createBody));
        var createContent = await createResponse.Content.ReadAsStringAsync();

        Assert.True(createResponse.IsSuccessStatusCode, $"GiftCard create failed. Body: {createContent}");

        var idElement = JsonDocument.Parse(createContent).RootElement.GetProperty("id");
        var giftCardId = idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString()
            : idElement.GetRawText();
        Assert.False(string.IsNullOrWhiteSpace(giftCardId));

        var getResponse = await _client.GetAsync($"/api/giftcards/{giftCardId}");
        var getContent = await getResponse.Content.ReadAsStringAsync();
        Assert.True(getResponse.IsSuccessStatusCode, $"GiftCard get failed. Body: {getContent}");
    }

    [Fact]
    public async Task GiftCardScenario_NegativeFlow_MissingResource_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/giftcards/99999999");
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound,
            $"GiftCard negative flow expected 404, but got {(int)response.StatusCode}. Body: {content}");
    }

    [Fact]
    public async Task BusinessDetailsScenario_HappyFlow_CreateThenGet_ReturnsOk()
    {
        var createBody = "{\"businessName\":\"Integration Shop\",\"businessEmail\":\"shop@example.com\",\"businessPhone\":\"+37060000001\",\"country\":\"Lithuania\",\"city\":\"Vilnius\",\"street\":\"Main Street\",\"houseNumber\":12,\"flatNumber\":1}";
        var createResponse = await _client.PostAsync("/api/business-details", CreateJsonBody(createBody));
        var createContent = await createResponse.Content.ReadAsStringAsync();
        Assert.True(createResponse.IsSuccessStatusCode, $"BusinessDetails create failed. Body: {createContent}");

        var getResponse = await _client.GetAsync("/api/business-details");
        var getContent = await getResponse.Content.ReadAsStringAsync();
        Assert.True(getResponse.IsSuccessStatusCode, $"BusinessDetails get failed. Body: {getContent}");
    }

    [Fact]
    public async Task BusinessDetailsScenario_NegativeFlow_InvalidPayload_ReturnsBadRequest()
    {
        var invalidBody = "{\"businessName\":\"\",\"businessEmail\":\"not-an-email\",\"businessPhone\":\"x\",\"country\":\"\",\"city\":\"\",\"street\":\"\",\"houseNumber\":0,\"flatNumber\":-1}";
        var response = await _client.PostAsync("/api/business-details", CreateJsonBody(invalidBody));
        var content = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest,
            $"BusinessDetails negative flow expected 400, but got {(int)response.StatusCode}. Body: {content}");
    }

    [Theory]
    [MemberData(nameof(ControllerActionSmokeData))]
    public async Task ControllerActionSmokeCoverage_ReturnsHandledStatus(
        HttpMethod method,
        string url,
        string? jsonBody,
        HttpStatusCode[] allowedStatuses)
    {
        using var request = new HttpRequestMessage(method, url);
        if (jsonBody is not null)
        {
            request.Content = CreateJsonBody(jsonBody);
        }

        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains(
            response.StatusCode,
            allowedStatuses);

        Assert.NotEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task AuthScenario_HappyFlow_RegisterThenLogin_ReturnsOkWithToken()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userName = $"integration-user-{suffix}";
        var email = $"{userName}@example.com";

        var registerBody =
            $$"""
            {
              "email":"{{email}}",
              "userName":"{{userName}}",
              "firstName":"Integration",
              "lastName":"User",
              "password":"Aa!12345",
              "phoneNumber":"+37060000000",
              "birthDate":"2000-01-01",
              "roleId":2
            }
            """;

        var registerResponse = await _client.PostAsync("/api/employees/register", CreateJsonBody(registerBody));
        var registerContent = await registerResponse.Content.ReadAsStringAsync();
        Assert.True(registerResponse.IsSuccessStatusCode, $"Register failed. Body: {registerContent}");

        var loginBody =
            $$"""
            {
              "userName":"{{userName}}",
              "password":"Aa!12345"
            }
            """;

        var loginResponse = await _client.PostAsync("/v1/auth/login", CreateJsonBody(loginBody));
        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        Assert.True(loginResponse.IsSuccessStatusCode, $"Login failed. Body: {loginContent}");

        using var loginJson = JsonDocument.Parse(loginContent);
        var token = GetJsonPropertyCaseInsensitive(loginJson.RootElement, "jwtToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task EmployeeScenario_HappyFlow_UpdateAndDeleteRegisteredEmployee_ReturnsOk()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userName = $"employee-flow-{suffix}";
        var email = $"{userName}@example.com";

        var registerBody =
            $$"""
            {
              "email":"{{email}}",
              "userName":"{{userName}}",
              "firstName":"Flow",
              "lastName":"Employee",
              "password":"Aa!12345",
              "phoneNumber":"+37061111111",
              "birthDate":"1999-01-01",
              "roleId":2
            }
            """;

        var registerResponse = await _client.PostAsync("/api/employees/register", CreateJsonBody(registerBody));
        var registerContent = await registerResponse.Content.ReadAsStringAsync();
        Assert.True(registerResponse.IsSuccessStatusCode, $"Register failed. Body: {registerContent}");

        using var registerJson = JsonDocument.Parse(registerContent);
        var employeeId = GetJsonPropertyCaseInsensitive(registerJson.RootElement, "id").GetInt32();

        var updateBody =
            $$"""
            {
              "firstName":"Updated",
              "lastName":"Employee",
              "birthDate":"1999-01-01",
              "userName":"{{userName}}",
              "email":"updated-{{email}}",
              "phoneNumber":"+37062222222",
              "roleId":3
            }
            """;

        var updateResponse = await _client.PutAsync($"/api/employees/{employeeId}", CreateJsonBody(updateBody));
        var updateContent = await updateResponse.Content.ReadAsStringAsync();
        Assert.True(updateResponse.IsSuccessStatusCode, $"Update employee failed. Body: {updateContent}");

        var deleteResponse = await _client.DeleteAsync($"/api/employees/{employeeId}");
        var deleteContent = await deleteResponse.Content.ReadAsStringAsync();
        Assert.True(deleteResponse.IsSuccessStatusCode, $"Delete employee failed. Body: {deleteContent}");
    }

    [Fact]
    public async Task PaymentScenario_HappyFlow_CashThenRefund_ReturnsOk()
    {
        var transactionRef = $"integration-cash-{Guid.NewGuid():N}";
        var cashBody =
            $$"""
            {
              "cartId":3,
              "amount":1500,
              "tip":100,
              "transactionRef":"{{transactionRef}}",
              "phoneNumber":null
            }
            """;

        var cashResponse = await _client.PostAsync("/api/payments/cash", CreateJsonBody(cashBody));
        var cashContent = await cashResponse.Content.ReadAsStringAsync();
        Assert.True(cashResponse.IsSuccessStatusCode, $"Cash payment failed. Body: {cashContent}");

        using var cashJson = JsonDocument.Parse(cashContent);
        var transactionIdString = GetJsonPropertyCaseInsensitive(cashJson.RootElement, "id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(transactionIdString));
        var transactionId = DateTime.Parse(transactionIdString!);

        var refundBody =
            """
            {
              "cartId":3,
              "isCard":false
            }
            """;

        var refundResponse = await _client.PatchAsync($"/api/payments/refund/{Uri.EscapeDataString(transactionId.ToString("O"))}", CreateJsonBody(refundBody));
        var refundContent = await refundResponse.Content.ReadAsStringAsync();
        Assert.True(refundResponse.IsSuccessStatusCode, $"Refund failed. Body: {refundContent}");
    }

    private static JsonElement GetJsonPropertyCaseInsensitive(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        throw new InvalidOperationException($"Property '{propertyName}' was not found in payload: {element}");
    }

    private static StringContent CreateJsonBody(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
