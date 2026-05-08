using Stripe;

namespace POS_System.Business.Services.Interfaces;

public interface IStripeCouponService
{
    Task<Coupon> CreateAsync(CouponCreateOptions options, CancellationToken cancellationToken);
    Task<Coupon> GetAsync(string couponId, CancellationToken cancellationToken);
    Task DeleteAsync(string couponId, CancellationToken cancellationToken);
}