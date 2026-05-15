using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders;

public sealed class ItemDiscountBuilder
{
    private int _value = 10;
    private bool _isPercentage = true;
    private string _description = $"Discount_{Guid.NewGuid().ToString("N")[..8]}";
    private DateTime? _startDate = null;
    private DateTime? _endDate = null;

    public ItemDiscountBuilder WithValue(int value) { _value = value; return this; }
    public ItemDiscountBuilder WithDescription(string desc) { _description = desc; return this; }
    public ItemDiscountBuilder WithIsPercentage(bool pct) { _isPercentage = pct; return this; }

    public ItemDiscountRequest Build() => new()
    {
        Value = _value,
        IsPercentage = _isPercentage,
        Description = _description,
        StartDate = _startDate,
        EndDate = _endDate
    };
}
