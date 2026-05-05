using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders;

internal sealed class GiftCardBuilder
{
    private DateOnly _date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
    private int _value = 500;

    public GiftCardBuilder WithDate(DateOnly date) { _date = date; return this; }
    public GiftCardBuilder WithValue(int value) { _value = value; return this; }

    public GiftCardRequest Build() => new()
    {
        Date = _date,
        Value = _value
    };
}
