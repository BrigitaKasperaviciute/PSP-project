using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Infrastructure.Builders;

public sealed class TaxRequestBuilder
{
    private string _name = $"Tax-{Guid.NewGuid():N}";
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
}

public sealed class ProductRequestBuilder
{
    private string _name = $"Product-{Guid.NewGuid():N}";
    private string _description = "Test product description";
    private int _price = 1000;
    private string _imageUrl = "https://example.com/image.jpg";
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
}

public sealed class ServiceRequestBuilder
{
    private string _name = $"Service-{Guid.NewGuid():N}";
    private string _description = "Test service description";
    private int _duration = 60;
    private int _price = 5000;
    private string _imageUrl = "https://example.com/service.jpg";
    private int _employeeId = 1;

    public ServiceRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ServiceRequestBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public ServiceRequestBuilder WithDuration(int duration)
    {
        _duration = duration;
        return this;
    }

    public ServiceRequestBuilder WithPrice(int price)
    {
        _price = price;
        return this;
    }

    public ServiceRequestBuilder WithImageUrl(string imageUrl)
    {
        _imageUrl = imageUrl;
        return this;
    }

    public ServiceRequestBuilder WithEmployeeId(int employeeId)
    {
        _employeeId = employeeId;
        return this;
    }

    public ServiceRequest Build()
    {
        return new ServiceRequest
        {
            Name = _name,
            Description = _description,
            Duration = _duration,
            Price = _price,
            ImageURL = _imageUrl,
            EmployeeId = _employeeId
        };
    }
}

public sealed class ProductModificationRequestBuilder
{
    private int _productVersionId = 1;
    private string _name = $"Modification-{Guid.NewGuid():N}";
    private string _description = "Test modification description";
    private int _price = 500;

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
}

public sealed class ItemDiscountRequestBuilder
{
    private int _value = 10;
    private bool _isPercentage = true;
    private string _description = $"Discount-{Guid.NewGuid():N}";
    private DateTime? _startDate = null;
    private DateTime? _endDate = null;

    public ItemDiscountRequestBuilder WithValue(int value)
    {
        _value = value;
        return this;
    }

    public ItemDiscountRequestBuilder WithIsPercentage(bool isPercentage)
    {
        _isPercentage = isPercentage;
        return this;
    }

    public ItemDiscountRequestBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public ItemDiscountRequestBuilder WithActiveNow()
    {
        _startDate = null;
        _endDate = null;
        return this;
    }

    public ItemDiscountRequestBuilder WithFutureDates()
    {
        _startDate = DateTime.UtcNow.AddDays(1);
        _endDate = DateTime.UtcNow.AddDays(30);
        return this;
    }

    public ItemDiscountRequestBuilder WithCustomDates(DateTime? start, DateTime? end)
    {
        _startDate = start;
        _endDate = end;
        return this;
    }

    public ItemDiscountRequest Build()
    {
        return new ItemDiscountRequest
        {
            Value = _value,
            IsPercentage = _isPercentage,
            Description = _description,
            StartDate = _startDate,
            EndDate = _endDate
        };
    }
}

public sealed class GiftCardRequestBuilder
{
    private DateOnly _date = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1));
    private int _value = 10000;

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
}

public sealed class CartRequestBuilder
{
    private int _employeeVersionId = 1;

    public CartRequestBuilder WithEmployeeVersionId(int employeeVersionId)
    {
        _employeeVersionId = employeeVersionId;
        return this;
    }

    public CartRequest Build()
    {
        return new CartRequest
        {
            EmployeeVersionId = _employeeVersionId
        };
    }
}

public sealed class CartItemRequestBuilder
{
    private int _cartId = 1;
    private int _quantity = 1;
    private bool _isProduct = true;
    private int? _productVersionId = 1;
    private int? _serviceVersionId = null;

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
}

public sealed class TimeSlotRequestBuilder
{
    private int _employeeVersionId = 1;
    private DateTime _startTime = DateTime.UtcNow.AddHours(1);
    private bool _isAvailable = true;

    public TimeSlotRequestBuilder WithEmployeeVersionId(int employeeVersionId)
    {
        _employeeVersionId = employeeVersionId;
        return this;
    }

    public TimeSlotRequestBuilder WithStartTime(DateTime startTime)
    {
        _startTime = startTime;
        return this;
    }

    public TimeSlotRequestBuilder WithIsAvailable(bool isAvailable)
    {
        _isAvailable = isAvailable;
        return this;
    }

    public TimeSlotRequest Build()
    {
        return new TimeSlotRequest
        {
            EmployeeVersionId = _employeeVersionId,
            StartTime = _startTime,
            IsAvailable = _isAvailable
        };
    }
}

public sealed class EmployeeRequestBuilder
{
    private string _firstName = $"First-{Guid.NewGuid():N}";
    private string _lastName = $"Last-{Guid.NewGuid():N}";
    private DateOnly _birthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30));
    private string _userName = $"user-{Guid.NewGuid():N}";
    private string _email = $"test-{Guid.NewGuid():N}@example.com";
    private string _phoneNumber = "+1234567890";
    private int _roleId = 2; // Default to User role

    public EmployeeRequestBuilder WithFirstName(string firstName)
    {
        _firstName = firstName;
        return this;
    }

    public EmployeeRequestBuilder WithLastName(string lastName)
    {
        _lastName = lastName;
        return this;
    }

    public EmployeeRequestBuilder WithBirthDate(DateOnly birthDate)
    {
        _birthDate = birthDate;
        return this;
    }

    public EmployeeRequestBuilder WithUserName(string userName)
    {
        _userName = userName;
        return this;
    }

    public EmployeeRequestBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public EmployeeRequestBuilder WithPhoneNumber(string phoneNumber)
    {
        _phoneNumber = phoneNumber;
        return this;
    }

    public EmployeeRequestBuilder WithRoleId(int roleId)
    {
        _roleId = roleId;
        return this;
    }

    public EmployeeRequest Build()
    {
        return new EmployeeRequest(
            _firstName,
            _lastName,
            _birthDate,
            _userName,
            _email,
            _phoneNumber,
            _roleId
        );
    }
}

public sealed class BusinessDetailsRequestBuilder
{
    private string _businessName = "Test Business";
    private string _businessEmail = "business@example.com";
    private string _businessPhone = "+1234567890";
    private string _country = "US";
    private string _city = "Test City";
    private string _street = "Main Street";
    private int _houseNumber = 123;
    private int? _flatNumber = null;

    public BusinessDetailsRequestBuilder WithBusinessName(string name)
    {
        _businessName = name;
        return this;
    }

    public BusinessDetailsRequestBuilder WithBusinessEmail(string email)
    {
        _businessEmail = email;
        return this;
    }

    public BusinessDetailsRequestBuilder WithBusinessPhone(string phone)
    {
        _businessPhone = phone;
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
}

public sealed class ServiceReservationRequestBuilder
{
    private int _cartItemId = 2;
    private int? _timeSlotId = 1;
    private DateTime _bookingTime = DateTime.UtcNow;
    private string _customerName = "Customer Name";
    private string _customerPhone = "+420123456789";
    private bool _isCancelled;

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
}

public sealed class CartDiscountRequestBuilder
{
    private int _value = 10;
    private bool _isPercentage = true;
    private DateTime _endDate = DateTime.UtcNow.AddDays(30);

    public CartDiscountRequestBuilder WithValue(int value)
    {
        _value = value;
        return this;
    }

    public CartDiscountRequestBuilder WithIsPercentage(bool isPercentage)
    {
        _isPercentage = isPercentage;
        return this;
    }

    public CartDiscountRequestBuilder WithEndDate(DateTime endDate)
    {
        _endDate = endDate;
        return this;
    }

    public CartDiscountRequest Build()
    {
        return new CartDiscountRequest
        {
            Value = _value,
            IsPercentage = _isPercentage,
            EndDate = _endDate
        };
    }
}

public sealed class CashRequestBuilder
{
    private int _cartId = 1;
    private ulong _amount = 1000;
    private int? _tip = null;
    private string _transactionRef = $"CASH-{Guid.NewGuid():N}";
    private string? _phoneNumber = null;

    public CashRequestBuilder WithCartId(int cartId)
    {
        _cartId = cartId;
        return this;
    }

    public CashRequestBuilder WithAmount(ulong amount)
    {
        _amount = amount;
        return this;
    }

    public CashRequestBuilder WithTip(int? tip)
    {
        _tip = tip;
        return this;
    }

    public CashRequestBuilder WithTransactionRef(string transactionRef)
    {
        _transactionRef = transactionRef;
        return this;
    }

    public CashRequestBuilder WithPhoneNumber(string? phoneNumber)
    {
        _phoneNumber = phoneNumber;
        return this;
    }

    public CashRequest Build()
    {
        return new CashRequest(_cartId, _amount, _tip, _transactionRef, _phoneNumber);
    }
}
