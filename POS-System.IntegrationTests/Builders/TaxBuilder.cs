using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders;

internal sealed class TaxBuilder
{
    private string _name = $"Tax-{Guid.NewGuid().ToString("N")[..8]}";
    private int _rate = 10;
    private bool _isPercentage = true;

    public TaxBuilder WithName(string name) { _name = name; return this; }
    public TaxBuilder WithRate(int rate) { _rate = rate; return this; }
    public TaxBuilder WithIsPercentage(bool isPercentage) { _isPercentage = isPercentage; return this; }

    public TaxRequest Build() => new()
    {
        Name = _name,
        Rate = _rate,
        IsPercentage = _isPercentage
    };
}
