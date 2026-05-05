using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders;

internal sealed class ProductBuilder
{
    private string _name = $"Product-{Guid.NewGuid():N}";
    private string _description = "Test product description";
    private int _price = 999;
    private string _imageUrl = "https://example.com/image.jpg";
    private int _stock = 10;

    public ProductBuilder WithName(string name) { _name = name; return this; }
    public ProductBuilder WithPrice(int price) { _price = price; return this; }
    public ProductBuilder WithStock(int stock) { _stock = stock; return this; }

    public ProductRequest Build() => new()
    {
        Name = _name,
        Description = _description,
        Price = _price,
        ImageURL = _imageUrl,
        Stock = _stock
    };
}
