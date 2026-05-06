using System.Collections.Concurrent;
using Stripe;

namespace POS_System.IntegrationTests.Infrastructure;

/// <summary>
/// In-memory Stripe client for integration tests.
/// Handles coupon CRUD without making real HTTP calls to Stripe.
/// Set via StripeConfiguration.StripeClient in ApiFactory.
/// </summary>
internal sealed class FakeStripeClient : IStripeClient
{
    private readonly ConcurrentDictionary<string, Coupon> _coupons = new();

    public string ApiBase => "https://api.stripe.com";
    public string ApiKey => "sk_test_fake";
    public string ClientId => string.Empty;
    public string ConnectBase => "https://connect.stripe.com";
    public string FilesBase => "https://files.stripe.com";
    public string MeterEventsBase => "https://meter-events.stripe.com";

    public Task<T> RequestAsync<T>(
        HttpMethod method,
        string path,
        BaseOptions options,
        RequestOptions requestOptions,
        CancellationToken cancellationToken = default)
        where T : IStripeEntity
    {
        IStripeEntity result;

        if (method == HttpMethod.Post && path == "/v1/coupons")
        {
            var opts = (CouponCreateOptions)options;
            var coupon = new Coupon
            {
                Id = Guid.NewGuid().ToString("N"),
                PercentOff = opts.PercentOff,
                AmountOff = opts.AmountOff,
                Duration = opts.Duration ?? "forever",
                Currency = opts.Currency ?? "EUR",
                Valid = true,
            };
            _coupons[coupon.Id] = coupon;
            result = coupon;
        }
        else if (method == HttpMethod.Get && path.StartsWith("/v1/coupons/"))
        {
            var id = path["/v1/coupons/".Length..];
            if (_coupons.TryGetValue(id, out var coupon))
                result = coupon;
            else
                throw new StripeException("No such coupon: " + id);
        }
        else if (method == HttpMethod.Delete && path.StartsWith("/v1/coupons/"))
        {
            var id = path["/v1/coupons/".Length..];
            _coupons.TryRemove(id, out _);
            result = new Coupon { Id = id };
        }
        else
        {
            throw new NotImplementedException($"FakeStripeClient: unhandled {method} {path}");
        }

        return Task.FromResult((T)result);
    }

    public Task<Stream> RequestStreamingAsync(
        HttpMethod method,
        string path,
        BaseOptions options,
        RequestOptions requestOptions,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("RequestStreamingAsync not implemented in FakeStripeClient");
}
