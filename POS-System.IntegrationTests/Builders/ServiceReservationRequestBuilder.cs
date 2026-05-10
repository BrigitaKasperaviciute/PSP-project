using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders
{
    public class ServiceReservationRequestBuilder
    {
        private int _cartItemId = 1;
        private int? _timeSlotId = 1;
        private DateTime _bookingTime = DateTime.UtcNow.AddDays(1);
        private string _customerName = "John Doe";
        private string _customerPhone = "1234567890";
        private bool _isCancelled = false;

        public ServiceReservationRequestBuilder WithCartItemId(int cartItemId)
        {
            _cartItemId = cartItemId;
            return this;
        }

        public ServiceReservationRequestBuilder WithTimeSlotId(int? timeSlotId)
        {
            _timeSlotId = timeSlotId;
            return this;
        }

        public ServiceReservationRequestBuilder WithBookingTime(DateTime bookingTime)
        {
            _bookingTime = bookingTime;
            return this;
        }

        public ServiceReservationRequestBuilder WithCustomerName(string customerName)
        {
            _customerName = customerName;
            return this;
        }

        public ServiceReservationRequestBuilder WithCustomerPhone(string customerPhone)
        {
            _customerPhone = customerPhone;
            return this;
        }

        public ServiceReservationRequestBuilder WithIsCancelled(bool isCancelled)
        {
            _isCancelled = isCancelled;
            return this;
        }

        public ServiceReservationRequest Build()
        {
            return new ServiceReservationRequest
            {
                CartItemId = _cartItemId,
                TimeSlotId = _timeSlotId,
                BookingTime = _bookingTime,
                CustomerName = _customerName,
                CustomerPhone = _customerPhone,
                IsCancelled = _isCancelled
            };
        }

        public static ServiceReservationRequest CreateDefault()
        {
            return new ServiceReservationRequestBuilder().Build();
        }
    }
}