using POS_System.Business.Services.Interfaces;
using POS_System.Business.Services.Models;
using Stripe;

namespace POS_System.Business.Services.Services;

public sealed class StripeCouponService : ICouponService
{
    private readonly CouponService _couponService = new CouponService();

    public async Task<CouponResult?> CreateAsync(CouponCreateOptions options, CancellationToken cancellationToken = default)
    {
        var coupon = await _couponService.CreateAsync(options, cancellationToken: cancellationToken);
        if (coupon is null) return null;

        return new CouponResult
        {
            Id = coupon.Id,
            AmountOff = coupon.AmountOff is null ? null : (long?)coupon.AmountOff,
            PercentOff = coupon.PercentOff is null ? null : (long?)coupon.PercentOff
        };
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _couponService.DeleteAsync(id);
    }
}
