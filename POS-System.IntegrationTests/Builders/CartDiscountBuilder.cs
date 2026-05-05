using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders;

internal sealed class CartDiscountBuilder
{
    private int _value = 15;
    private bool _isPercentage = true;
    private DateTime? _endDate = DateTime.UtcNow.AddDays(30);

    public CartDiscountBuilder WithValue(int value) { _value = value; return this; }
    public CartDiscountBuilder WithIsPercentage(bool isPercentage) { _isPercentage = isPercentage; return this; }
    public CartDiscountBuilder WithEndDate(DateTime? endDate) { _endDate = endDate; return this; }

    public CartDiscountRequest Build() => new()
    {
        Value = _value,
        IsPercentage = _isPercentage,
        EndDate = _endDate
    };
}
