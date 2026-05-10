using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders
{
    public class ProductModificationRequestBuilder
    {
        private int _productVersionId = 1;
        private string _name = "Extra Cheese";
        private string _description = "Add extra cheese";
        private int _price = 200;

        public ProductModificationRequestBuilder WithProductVersionId(int productVersionId)
        {
            _productVersionId = productVersionId;
            return this;
        }

        public ProductModificationRequestBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public ProductModificationRequestBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        public ProductModificationRequestBuilder WithPrice(int price)
        {
            _price = price;
            return this;
        }

        public ProductModificationRequest Build()
        {
            return new ProductModificationRequest
            {
                ProductVersionId = _productVersionId,
                Name = _name,
                Description = _description,
                Price = _price
            };
        }

        public static ProductModificationRequest CreateDefault()
        {
            return new ProductModificationRequestBuilder().Build();
        }
    }
}