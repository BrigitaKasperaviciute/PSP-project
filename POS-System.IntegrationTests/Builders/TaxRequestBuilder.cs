using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders
{
    /// <summary>
    /// Builder for creating TaxRequest test data.
    /// </summary>
    public class TaxRequestBuilder
    {
        private string _name = "Test Tax";
        private int _rate = 10;
        private bool _isPercentage = true;

        public TaxRequestBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public TaxRequestBuilder WithRate(int rate)
        {
            _rate = rate;
            return this;
        }

        public TaxRequestBuilder WithIsPercentage(bool isPercentage)
        {
            _isPercentage = isPercentage;
            return this;
        }

        public TaxRequest Build()
        {
            return new TaxRequest
            {
                Name = _name,
                Rate = _rate,
                IsPercentage = _isPercentage
            };
        }

        public static TaxRequest CreateDefault()
        {
            return new TaxRequestBuilder().Build();
        }

        public static TaxRequest CreateWithName(string name)
        {
            return new TaxRequestBuilder().WithName(name).Build();
        }

        public static TaxRequest CreateWithRate(int rate)
        {
            return new TaxRequestBuilder().WithRate(rate).Build();
        }
    }
}
