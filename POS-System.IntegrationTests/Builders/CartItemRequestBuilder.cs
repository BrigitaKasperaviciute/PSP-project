using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders
{
    /// <summary>
    /// Builder for creating CartItemRequest test data.
    /// </summary>
    public class CartItemRequestBuilder
    {
        private int _cartId = 1;
        private int _quantity = 1;
        private bool _isProduct = true;
        private int? _productVersionId;
        private int? _serviceVersionId;

        public CartItemRequestBuilder WithCartId(int cartId)
        {
            _cartId = cartId;
            return this;
        }

        public CartItemRequestBuilder WithQuantity(int quantity)
        {
            _quantity = quantity;
            return this;
        }

        public CartItemRequestBuilder WithIsProduct(bool isProduct)
        {
            _isProduct = isProduct;
            return this;
        }

        public CartItemRequestBuilder WithProductVersionId(int? productVersionId)
        {
            _productVersionId = productVersionId;
            return this;
        }

        public CartItemRequestBuilder WithServiceVersionId(int? serviceVersionId)
        {
            _serviceVersionId = serviceVersionId;
            return this;
        }

        public CartItemRequest Build()
        {
            return new CartItemRequest
            {
                CartId = _cartId,
                Quantity = _quantity,
                IsProduct = _isProduct,
                ProductVersionId = _productVersionId,
                ServiceVersionId = _serviceVersionId
            };
        }

        public static CartItemRequest CreateDefault()
        {
            return new CartItemRequestBuilder().Build();
        }

        public static CartItemRequest CreateWithProductVersion(int cartId, int productVersionId)
        {
            return new CartItemRequestBuilder()
                .WithCartId(cartId)
                .WithIsProduct(true)
                .WithProductVersionId(productVersionId)
                .Build();
        }

        public static CartItemRequest CreateWithServiceVersion(int cartId, int serviceVersionId)
        {
            return new CartItemRequestBuilder()
                .WithCartId(cartId)
                .WithIsProduct(false)
                .WithServiceVersionId(serviceVersionId)
                .Build();
        }
    }
}
