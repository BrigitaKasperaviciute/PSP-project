using POS_System.Business.Services.Interfaces;
using POS_System.Business.Services.Models;
using Stripe;

namespace POS_System.IntegrationTests.Infrastructure.MockServices;

public sealed class FakeCouponService : ICouponService
{
    public Task<CouponResult?> CreateAsync(CouponCreateOptions options, CancellationToken cancellationToken = default)
    {
        // Return a deterministic fake coupon result for tests
        var id = Guid.NewGuid().ToString();
        var result = new CouponResult
        {
            Id = id,
            AmountOff = options.AmountOff is null ? null : Convert.ToInt64(options.AmountOff.Value),
            PercentOff = options.PercentOff is null ? null : Convert.ToInt64(options.PercentOff.Value)
        };

        return Task.FromResult<CouponResult?>(result);
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        // no-op for tests
        return Task.CompletedTask;
    }
}
