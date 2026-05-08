using POS_System.Business.Services.Interfaces;
using Stripe;

namespace POS_System.Business.Services;

public sealed class StripeCouponService : IStripeCouponService
{
    private readonly CouponService _couponService = new();

    public Task<Coupon> CreateAsync(CouponCreateOptions options, CancellationToken cancellationToken)
        => _couponService.CreateAsync(options, cancellationToken: cancellationToken);

    public Task<Coupon> GetAsync(string couponId, CancellationToken cancellationToken)
        => _couponService.GetAsync(couponId, cancellationToken: cancellationToken);

    public Task DeleteAsync(string couponId, CancellationToken cancellationToken)
        => _couponService.DeleteAsync(couponId, cancellationToken: cancellationToken);
}