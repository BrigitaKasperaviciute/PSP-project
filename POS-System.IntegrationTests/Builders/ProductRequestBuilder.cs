using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders
{
    /// <summary>
    /// Builder for creating ProductRequest test data.
    /// </summary>
    public class ProductRequestBuilder
    {
        private string _name = "Test Product";
        private string _description = "Test Product Description";
        private int _price = 1000;
        private string _imageUrl = "https://example.com/product.jpg";
        private int _stock = 100;

        public ProductRequestBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public ProductRequestBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        public ProductRequestBuilder WithPrice(int price)
        {
            _price = price;
            return this;
        }

        public ProductRequestBuilder WithImageUrl(string imageUrl)
        {
            _imageUrl = imageUrl;
            return this;
        }

        public ProductRequestBuilder WithStock(int stock)
        {
            _stock = stock;
            return this;
        }

        public ProductRequest Build()
        {
            return new ProductRequest
            {
                Name = _name,
                Description = _description,
                Price = _price,
                ImageURL = _imageUrl,
                Stock = _stock
            };
        }

        public static ProductRequest CreateDefault()
        {
            return new ProductRequestBuilder().Build();
        }

        public static ProductRequest CreateWithName(string name)
        {
            return new ProductRequestBuilder().WithName(name).Build();
        }

        public static ProductRequest CreateWithPrice(int price)
        {
            return new ProductRequestBuilder().WithPrice(price).Build();
        }
    }
}
