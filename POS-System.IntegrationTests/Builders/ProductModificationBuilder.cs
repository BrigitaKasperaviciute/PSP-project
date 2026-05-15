using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders;

public sealed class ProductModificationBuilder
{
    private int _productVersionId = 0;
    private string _name = $"Mod_{Guid.NewGuid().ToString("N")[..8]}";
    private string _description = "Test modification description";
    private int _price = 500;

    public ProductModificationBuilder WithProductVersionId(int id) { _productVersionId = id; return this; }
    public ProductModificationBuilder WithName(string name) { _name = name; return this; }
    public ProductModificationBuilder WithPrice(int price) { _price = price; return this; }

    public ProductModificationRequest Build() => new()
    {
        ProductVersionId = _productVersionId,
        Name = _name,
        Description = _description,
        Price = _price
    };
}
