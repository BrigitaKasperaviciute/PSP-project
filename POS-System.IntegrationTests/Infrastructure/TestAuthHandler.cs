using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace POS_System.IntegrationTests.Infrastructure;

public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "IntegrationTestScheme";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("TransactionWrite", "Y"),
            new Claim("TransactionRead", "Y"),
            new Claim("HistoricTransactionWrite", "Y"),
            new Claim("HistoricTransactionRead", "Y"),
            new Claim("ServiceWrite", "Y"),
            new Claim("ServiceRead", "Y"),
            new Claim("ItemWrite", "Y"),
            new Claim("ItemRead", "Y"),
            new Claim("EmployeesWrite", "Y"),
            new Claim("EmployeesRead", "Y"),
            new Claim("TaxWrite", "Y"),
            new Claim("TaxRead", "Y"),
            new Claim("HistoricWrite", "Y"),
            new Claim("HistoricRead", "Y"),
            new Claim("GiftCardWrite", "Y"),
            new Claim("GiftCardRead", "Y"),
            new Claim("CartItemWrite", "Y"),
            new Claim("CartItemRead", "Y"),
            new Claim("ItemDiscountRead", "Y"),
            new Claim("ItemDiscountWrite", "Y"),
            new Claim("BusinessDetailsRead", "Y"),
            new Claim("BusinessDetailsWrite", "Y")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
