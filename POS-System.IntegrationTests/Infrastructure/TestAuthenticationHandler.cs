using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace POS_System.IntegrationTests.Infrastructure;

public class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return Task.FromResult(AuthenticateResult.NoResult());

        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Test ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "TestUser"),
            new(ClaimTypes.NameIdentifier, "1"),
            new("TransactionWrite", "true"),
            new("TransactionRead", "true"),
            new("HistoricTransactionWrite", "true"),
            new("HistoricTransactionRead", "true"),
            new("ServiceWrite", "true"),
            new("ServiceRead", "true"),
            new("ItemWrite", "true"),
            new("ItemRead", "true"),
            new("EmployeesWrite", "true"),
            new("EmployeesRead", "true"),
            new("TaxWrite", "true"),
            new("TaxRead", "true"),
            new("HistoricWrite", "true"),
            new("HistoricRead", "true"),
            new("GiftCardWrite", "true"),
            new("GiftCardRead", "true"),
            new("CartItemWrite", "true"),
            new("CartItemRead", "true"),
            new("ItemDiscountRead", "true"),
            new("ItemDiscountWrite", "true"),
            new("BusinessDetailsRead", "true"),
            new("BusinessDetailsWrite", "true"),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
