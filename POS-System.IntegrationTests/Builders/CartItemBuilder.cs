using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders;

internal sealed class CartItemBuilder
{
    private int _cartId;
    private int _quantity = 2;
    private bool _isProduct = true;
    private int? _productVersionId;
    private int? _serviceVersionId;

    public CartItemBuilder WithCartId(int cartId) { _cartId = cartId; return this; }
    public CartItemBuilder WithQuantity(int quantity) { _quantity = quantity; return this; }
    public CartItemBuilder WithProductVersionId(int id) { _productVersionId = id; _isProduct = true; return this; }
    public CartItemBuilder WithServiceVersionId(int id) { _serviceVersionId = id; _isProduct = false; return this; }

    public CartItemRequest Build() => new()
    {
        CartId = _cartId,
        Quantity = _quantity,
        IsProduct = _isProduct,
        ProductVersionId = _productVersionId,
        ServiceVersionId = _serviceVersionId
    };
}
