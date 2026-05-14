namespace POS_System.Business.Services.Models;

public sealed class CouponResult
{
    public string Id { get; set; } = null!;
    public long? AmountOff { get; set; }
    public long? PercentOff { get; set; }
}
