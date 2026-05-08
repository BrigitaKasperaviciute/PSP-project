using System.Collections.Concurrent;
using POS_System.Business.Services.Interfaces;
using Stripe;

namespace POS_System.IntegrationTests.Infrastructure;

public sealed class FakeStripeCouponService : IStripeCouponService
{
    private readonly ConcurrentDictionary<string, Coupon> _coupons = new();

    public Task<Coupon> CreateAsync(CouponCreateOptions options, CancellationToken cancellationToken)
    {
        var couponId = options.Currency is null ? Guid.NewGuid().ToString("N") : $"test_coupon_{Guid.NewGuid():N}";

        var coupon = new Coupon
        {
            Id = couponId,
            AmountOff = options.AmountOff,
            PercentOff = options.PercentOff,
            Valid = true
        };

        _coupons[couponId] = coupon;
        return Task.FromResult(coupon);
    }

    public Task<Coupon> GetAsync(string couponId, CancellationToken cancellationToken)
    {
        if (_coupons.TryGetValue(couponId, out var coupon))
        {
            return Task.FromResult(coupon);
        }

        return Task.FromResult(new Coupon
        {
            Id = couponId,
            Valid = false
        });
    }

    public Task DeleteAsync(string couponId, CancellationToken cancellationToken)
    {
        _coupons.TryRemove(couponId, out _);
        return Task.CompletedTask;
    }
}