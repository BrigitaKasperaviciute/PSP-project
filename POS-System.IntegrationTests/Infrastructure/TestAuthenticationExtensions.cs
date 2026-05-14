using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace POS_System.IntegrationTests.Infrastructure;

public static class TestAuthenticationExtensions
{
    public static IServiceCollection AddTestAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, options => { });

        services.AddAuthorization(options =>
        {
            // Match the app's policy names exactly.
            options.AddPolicy("ServiceRead", policy => policy.RequireClaim("ServiceRead"));
            options.AddPolicy("ServiceWrite", policy => policy.RequireClaim("ServiceWrite"));
            options.AddPolicy("ItemRead", policy => policy.RequireClaim("ItemRead"));
            options.AddPolicy("ItemWrite", policy => policy.RequireClaim("ItemWrite"));
            options.AddPolicy("ItemDiscountRead", policy => policy.RequireClaim("ItemDiscountRead"));
            options.AddPolicy("ItemDiscountWrite", policy => policy.RequireClaim("ItemDiscountWrite"));
            options.AddPolicy("TaxRead", policy => policy.RequireClaim("TaxRead"));
            options.AddPolicy("TaxWrite", policy => policy.RequireClaim("TaxWrite"));
            options.AddPolicy("BusinessDetailsRead", policy => policy.RequireClaim("BusinessDetailsRead"));
            options.AddPolicy("BusinessDetailsWrite", policy => policy.RequireClaim("BusinessDetailsWrite"));
            options.AddPolicy("EmployeesRead", policy => policy.RequireClaim("EmployeesRead"));
            options.AddPolicy("EmployeesWrite", policy => policy.RequireClaim("EmployeesWrite"));
            options.AddPolicy("GiftCardRead", policy => policy.RequireClaim("GiftCardRead"));
            options.AddPolicy("GiftCardWrite", policy => policy.RequireClaim("GiftCardWrite"));
            options.AddPolicy("CartItemRead", policy => policy.RequireClaim("CartItemRead"));
            options.AddPolicy("CartItemWrite", policy => policy.RequireClaim("CartItemWrite"));
        });

        return services;
    }
}

public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user"),
            new(ClaimTypes.Email, "test@example.com"),
            new("ServiceRead", "true"),
            new("ServiceWrite", "true"),
            new("ItemRead", "true"),
            new("ItemWrite", "true"),
            new("ItemDiscountRead", "true"),
            new("ItemDiscountWrite", "true"),
            new("TaxRead", "true"),
            new("TaxWrite", "true"),
            new("BusinessDetailsRead", "true"),
            new("BusinessDetailsWrite", "true"),
            new("EmployeesRead", "true"),
            new("EmployeesWrite", "true"),
            new("GiftCardRead", "true"),
            new("GiftCardWrite", "true"),
            new("CartItemRead", "true"),
            new("CartItemWrite", "true")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
