using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders;

internal sealed class ItemDiscountBuilder
{
    private int _value = 10;
    private bool _isPercentage = true;
    private string _description = "Test discount";
    private DateTime? _startDate = null;
    private DateTime? _endDate = null;

    public ItemDiscountBuilder WithValue(int value) { _value = value; return this; }
    public ItemDiscountBuilder WithIsPercentage(bool isPercentage) { _isPercentage = isPercentage; return this; }
    public ItemDiscountBuilder WithDescription(string description) { _description = description; return this; }
    public ItemDiscountBuilder WithEndDate(DateTime endDate) { _endDate = endDate; return this; }

    public ItemDiscountRequest Build() => new()
    {
        Value = _value,
        IsPercentage = _isPercentage,
        Description = _description,
        StartDate = _startDate,
        EndDate = _endDate
    };
}
