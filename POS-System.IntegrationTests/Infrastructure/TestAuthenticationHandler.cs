using System.Security.Claims;
using System;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace POS_System.IntegrationTests.Infrastructure;

/// <summary>
/// Custom authentication scheme for integration tests.
/// Allows tests to inject any claims they need into the JWT token.
/// </summary>
public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string TestScheme = "Test";

    #pragma warning disable CS0618
    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TimeProvider timeProvider)
        : base(options, logger, encoder, new TimeProviderSystemClockAdapter(timeProvider))
    #pragma warning restore CS0618
    {
    }


#pragma warning disable CS0618
internal sealed class TimeProviderSystemClockAdapter : Microsoft.AspNetCore.Authentication.ISystemClock
#pragma warning restore CS0618
{
    private readonly TimeProvider _timeProvider;

    public TimeProviderSystemClockAdapter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();
}
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>();

        // Extract claims from headers
        if (Context.Request.Headers.TryGetValue("X-Test-Claims", out var claimsHeader))
        {
            var claimStrings = claimsHeader.ToString().Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var claim in claimStrings)
            {
                var parts = claim.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    claims.Add(new Claim(parts[0], parts[1]));
                }
            }
        }

        // Add default admin claims if no claims provided (for backward compatibility)
        if (claims.Count == 0)
        {
            claims.AddRange(new[]
            {
                new Claim("TransactionWrite", "true"),
                new Claim("TransactionRead", "true"),
                new Claim("ServiceWrite", "true"),
                new Claim("ServiceRead", "true"),
                new Claim("ItemWrite", "true"),
                new Claim("ItemRead", "true"),
                new Claim("EmployeesWrite", "true"),
                new Claim("EmployeesRead", "true"),
                new Claim("TaxWrite", "true"),
                new Claim("TaxRead", "true"),
                new Claim("GiftCardWrite", "true"),
                new Claim("GiftCardRead", "true"),
                new Claim("CartItemWrite", "true"),
                new Claim("CartItemRead", "true"),
                new Claim("ItemDiscountRead", "true"),
                new Claim("ItemDiscountWrite", "true"),
                new Claim("BusinessDetailsRead", "true"),
                new Claim("BusinessDetailsWrite", "true"),
                new Claim("HistoricTransactionWrite", "true"),
                new Claim("HistoricTransactionRead", "true"),
                new Claim("HistoricWrite", "true"),
                new Claim("HistoricRead", "true"),
            });
        }

        var identity = new ClaimsIdentity(claims, TestScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, TestScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
