using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders;

public sealed class CartDiscountBuilder
{
    private int _value = 10;
    private bool _isPercentage = true;
    private DateTime? _endDate = DateTime.UtcNow.AddMonths(3);

    public CartDiscountBuilder WithValue(int value) { _value = value; return this; }
    public CartDiscountBuilder WithIsPercentage(bool pct) { _isPercentage = pct; return this; }
    public CartDiscountBuilder WithEndDate(DateTime? endDate) { _endDate = endDate; return this; }

    public CartDiscountRequest Build() => new()
    {
        Value = _value,
        IsPercentage = _isPercentage,
        EndDate = _endDate
    };
}
