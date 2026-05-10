using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders
{
    public class BusinessDetailsRequestBuilder
    {
        private string _businessName = "Seeded Business";
        private string _businessEmail = "seeded@example.com";
        private string _businessPhone = "+37060000000";
        private string _country = "Lithuania";
        private string _city = "Vilnius";
        private string _street = "Main Street";
        private int _houseNumber = 1;
        private int? _flatNumber = null;

        public BusinessDetailsRequestBuilder WithBusinessName(string businessName)
        {
            _businessName = businessName;
            return this;
        }

        public BusinessDetailsRequestBuilder WithBusinessEmail(string businessEmail)
        {
            _businessEmail = businessEmail;
            return this;
        }

        public BusinessDetailsRequestBuilder WithBusinessPhone(string businessPhone)
        {
            _businessPhone = businessPhone;
            return this;
        }

        public BusinessDetailsRequestBuilder WithCountry(string country)
        {
            _country = country;
            return this;
        }

        public BusinessDetailsRequestBuilder WithCity(string city)
        {
            _city = city;
            return this;
        }

        public BusinessDetailsRequestBuilder WithStreet(string street)
        {
            _street = street;
            return this;
        }

        public BusinessDetailsRequestBuilder WithHouseNumber(int houseNumber)
        {
            _houseNumber = houseNumber;
            return this;
        }

        public BusinessDetailsRequestBuilder WithFlatNumber(int? flatNumber)
        {
            _flatNumber = flatNumber;
            return this;
        }

        public BusinessDetailsRequest Build()
        {
            return new BusinessDetailsRequest
            {
                BusinessName = _businessName,
                BusinessEmail = _businessEmail,
                BusinessPhone = _businessPhone,
                Country = _country,
                City = _city,
                Street = _street,
                HouseNumber = _houseNumber,
                FlatNumber = _flatNumber
            };
        }

        public static BusinessDetailsRequest CreateDefault()
        {
            return new BusinessDetailsRequestBuilder().Build();
        }
    }
}