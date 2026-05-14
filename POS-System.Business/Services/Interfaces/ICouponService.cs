using POS_System.Business.Services.Models;
using Stripe;

namespace POS_System.Business.Services.Interfaces;

public interface ICouponService
{
    Task<CouponResult?> CreateAsync(CouponCreateOptions options, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
