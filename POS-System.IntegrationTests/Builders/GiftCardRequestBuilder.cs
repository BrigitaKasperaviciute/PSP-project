using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders
{
    /// <summary>
    /// Builder for creating GiftCardRequest test data.
    /// </summary>
    public class GiftCardRequestBuilder
    {
        private DateOnly _date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
        private int _value = 5000;

        public GiftCardRequestBuilder WithDate(DateOnly date)
        {
            _date = date;
            return this;
        }

        public GiftCardRequestBuilder WithValue(int value)
        {
            _value = value;
            return this;
        }

        public GiftCardRequest Build()
        {
            return new GiftCardRequest
            {
                Date = _date,
                Value = _value
            };
        }

        public static GiftCardRequest CreateDefault()
        {
            return new GiftCardRequestBuilder().Build();
        }

        public static GiftCardRequest CreateWithValue(int value)
        {
            return new GiftCardRequestBuilder().WithValue(value).Build();
        }

        public static GiftCardRequest CreateExpired()
        {
            return new GiftCardRequestBuilder()
                .WithDate(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)))
                .Build();
        }
    }
}
