using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders;

public sealed class GiftCardBuilder
{
    private DateOnly _date = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3));
    private int _value = 5000;

    public GiftCardBuilder WithDate(DateOnly date) { _date = date; return this; }
    public GiftCardBuilder WithValue(int value) { _value = value; return this; }

    public GiftCardRequest Build() => new() { Date = _date, Value = _value };
}
