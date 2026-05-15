using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Builders;

public sealed class ServiceReservationBuilder
{
    private int _cartItemId = 0;
    private int? _timeSlotId = null;
    private DateTime _bookingTime = DateTime.UtcNow.AddDays(1);
    private string _customerName = "Test Customer";
    private string _customerPhone = "37060000001";
    private bool _isCancelled = false;

    public ServiceReservationBuilder WithCartItemId(int id) { _cartItemId = id; return this; }
    public ServiceReservationBuilder WithTimeSlotId(int id) { _timeSlotId = id; return this; }
    public ServiceReservationBuilder WithBookingTime(DateTime time) { _bookingTime = time; return this; }
    public ServiceReservationBuilder WithIsCancelled(bool cancelled) { _isCancelled = cancelled; return this; }
    public ServiceReservationBuilder WithCustomerName(string name) { _customerName = name; return this; }
    public ServiceReservationBuilder WithCustomerPhone(string phone) { _customerPhone = phone; return this; }
    public ServiceReservationBuilder WithCustomerNameAndPhone(string name, string phone) { _customerName = name; _customerPhone = phone; return this; }

    public ServiceReservationRequest Build() => new()
    {
        CartItemId = _cartItemId,
        TimeSlotId = _timeSlotId,
        BookingTime = _bookingTime,
        CustomerName = _customerName,
        CustomerPhone = _customerPhone,
        IsCancelled = _isCancelled
    };
}
