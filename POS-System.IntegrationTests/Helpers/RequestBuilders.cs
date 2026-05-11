using System;
using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Helpers;

/// <summary>
/// Builder for TaxRequest - provides fluent API for creating test tax data.
/// </summary>
public sealed class TaxRequestBuilder
{
    private string _name = $"Tax-{Guid.NewGuid():N}";
    private int _rate = 19;
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

    // Compatibility: accept decimal rates in tests
    public TaxRequestBuilder WithRate(decimal rate)
    {
        _rate = (int)Math.Round(rate);
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

/// <summary>
/// Builder for ProductRequest - provides fluent API for creating test product data.
/// </summary>
public sealed class ProductRequestBuilder
{
    private string _name = $"Product-{Guid.NewGuid():N}";
    private string _description = "Test product description";
    private int _price = 999;
    private string _imageUrl = "https://example.com/product.jpg";
    private int _stock = 10;
    private bool _isActive = true;

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

    // Accept integer prices as well as decimal
    public ProductRequestBuilder WithPrice(int price)
    {
        _price = price;
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

    // Compatibility overload used by older tests that passed decimal prices
    public ProductRequestBuilder WithPrice(decimal price)
    {
        _price = (int)Math.Round(price * 100);
        return this;
    }

    // Compatibility method - some tests set IsActive even though request doesn't have it anymore
    public ProductRequestBuilder WithIsActive(bool isActive)
    {
        _isActive = isActive;
        return this;
    }
}

/// <summary>
/// Builder for ServiceRequest - provides fluent API for creating test service data.
/// </summary>
public sealed class ServiceRequestBuilder
{
    private string _name = $"Service-{Guid.NewGuid():N}";
    private string _description = "Test service description";
    private int _duration = 30;
    private int _price = 2999;
    private string _imageUrl = "https://example.com/image.jpg";
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

    // Accept decimal price in tests (dollars -> cents)
    public ServiceRequestBuilder WithPrice(decimal price)
    {
        _price = (int)Math.Round(price * 100);
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

/// <summary>
/// Builder for CartRequest - provides fluent API for creating test cart data.
/// </summary>
public sealed class CartRequestBuilder
{
    private int _employeeVersionId = 1;
    private POS_System.Common.Enums.CartStatusEnum _status = POS_System.Common.Enums.CartStatusEnum.PENDING;

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
            , Status = _status
        };
    }

    public CartRequestBuilder WithStatus(string status)
    {
        if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            _status = POS_System.Common.Enums.CartStatusEnum.IN_PROGRESS;
            return this;
        }

        if (Enum.TryParse<POS_System.Common.Enums.CartStatusEnum>(status, true, out var parsed))
        {
            _status = parsed;
        }

        return this;
    }
}

/// <summary>
/// Builder for CartItemRequest - provides fluent API for creating test cart item data.
/// </summary>
public sealed class CartItemRequestBuilder
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
}

/// <summary>
/// Builder for TimeSlotRequest - provides fluent API for creating test time slot data.
/// </summary>
public sealed class TimeSlotRequestBuilder
{
    private DateTime _startTime = DateTime.UtcNow.AddDays(1).Date.AddHours(9);
    private DateTime? _endTime = null;
    private bool _isAvailable = true;
    private int _employeeVersionId = 1;

    public TimeSlotRequestBuilder WithStartTime(DateTime startTime)
    {
        _startTime = startTime;
        return this;
    }

    // Compatibility: older tests set an end time which the DTO no longer carries.
    public TimeSlotRequestBuilder WithEndTime(DateTime endTime)
    {
        _endTime = endTime;
        return this;
    }

    public TimeSlotRequestBuilder WithIsAvailable(bool isAvailable)
    {
        _isAvailable = isAvailable;
        return this;
    }

    public TimeSlotRequestBuilder WithEmployeeVersionId(int employeeVersionId)
    {
        _employeeVersionId = employeeVersionId;
        return this;
    }

    // Compatibility: some older tests use WithEmployeeId
    public TimeSlotRequestBuilder WithEmployeeId(int employeeId)
    {
        _employeeVersionId = employeeId;
        return this;
    }

    public TimeSlotRequest Build()
    {
        return new TimeSlotRequest
        {
            StartTime = _startTime,
            IsAvailable = _isAvailable,
            EmployeeVersionId = _employeeVersionId
        };
    }
}

/// <summary>
/// Builder for ItemDiscountRequest - provides fluent API for creating test item discount data.
/// </summary>
public sealed class ItemDiscountRequestBuilder
{
    private int _value = 10;
    private bool _isPercentage = true;
    private string _description = "Test discount";
    private DateTime? _startDate;
    private DateTime? _endDate;

    public ItemDiscountRequestBuilder WithValue(int value)
    {
        _value = value;
        return this;
    }

    // Accept decimal in tests and convert to cents
    // Compatibility overload for older tests passing decimal values (dollars -> cents)
    public ItemDiscountRequestBuilder WithValue(decimal value)
    {
        _value = (int)Math.Round(value * 100);
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

    public ItemDiscountRequestBuilder WithStartDate(DateTime? startDate)
    {
        _startDate = startDate;
        return this;
    }

    public ItemDiscountRequestBuilder WithEndDate(DateTime? endDate)
    {
        _endDate = endDate;
        return this;
    }

    public ItemDiscountRequestBuilder WithActiveDates()
    {
        _startDate = null;
        _endDate = null;
        return this;
    }

    public ItemDiscountRequestBuilder WithFutureDates()
    {
        _startDate = DateTime.UtcNow.AddDays(1);
        _endDate = DateTime.UtcNow.AddDays(7);
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

/// <summary>
/// Builder for GiftCardRequest - provides fluent API for creating test gift card data.
/// </summary>
public sealed class GiftCardRequestBuilder
{
    private DateOnly _date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
    private int _value = 50;

    public GiftCardRequestBuilder WithDate(DateOnly date)
    {
        _date = date;
        return this;
    }

    // Compatibility: accept DateTime as older tests used DateTime
    public GiftCardRequestBuilder WithDate(DateTime date)
    {
        _date = DateOnly.FromDateTime(date);
        return this;
    }

    public GiftCardRequestBuilder WithValue(int value)
    {
        _value = value;
        return this;
    }

    // Compatibility: accept decimal for value (dollars -> cents)
    public GiftCardRequestBuilder WithValue(decimal value)
    {
        _value = (int)Math.Round(value * 100);
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

/// <summary>
/// Builder for EmployeeRequest - provides fluent API for creating test employee data.
/// </summary>
public sealed class EmployeeRequestBuilder
{
    private string _firstName = $"Employee-{Guid.NewGuid():N}";
    private string _lastName = "Test";
    private string _userName = $"emp-{Guid.NewGuid():N}";
    private string _email = $"emp-{Guid.NewGuid():N}@example.com";
    private DateOnly _birthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-25));
    private string _phoneNumber = "1234567890";
    private int _roleId = 2;
    private bool _isActive = true;

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

    public EmployeeRequestBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public EmployeeRequestBuilder WithIsActive(bool isActive)
    {
        _isActive = isActive;
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
            _roleId);
    }
}

/// <summary>
/// Builder for UserRegisterRequest - provides fluent API for creating test user registration data.
/// </summary>
public sealed class UserRegisterRequestBuilder
{
    private string _email = $"user-{Guid.NewGuid():N}@example.com";
    private string _password = "Test@Password123";
    private string _firstName = "Test";
    private string _lastName = "User";
    private string _userName = $"user-{Guid.NewGuid():N}";
    private string _phoneNumber = "1234567890";
    private DateOnly _birthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30));
    private int _roleId = 2;

    public UserRegisterRequestBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public UserRegisterRequestBuilder WithPassword(string password)
    {
        _password = password;
        return this;
    }

    public UserRegisterRequestBuilder WithFirstName(string firstName)
    {
        _firstName = firstName;
        return this;
    }

    public UserRegisterRequestBuilder WithLastName(string lastName)
    {
        _lastName = lastName;
        return this;
    }

    public UserRegisterRequest Build()
    {
        return new UserRegisterRequest(
            _email,
            _userName,
            _firstName,
            _lastName,
            _password,
            _phoneNumber,
            _birthDate,
            _roleId);
    }
}

/// <summary>
/// Builder for ProductModificationRequest - provides fluent API for creating test product modification data.
/// </summary>
public sealed class ProductModificationRequestBuilder
{
    private int _productVersionId = 0;
    private string _name = $"PM-{Guid.NewGuid():N}";
    private string _description = "Test modification";
    private int _price = 599;

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

    // Compatibility: accept decimal price
    public ProductModificationRequestBuilder WithPrice(decimal price)
    {
        _price = (int)Math.Round(price * 100);
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

/// <summary>
/// Builder for BusinessDetailsRequest - provides fluent API for creating test business details data.
/// </summary>
public sealed class BusinessDetailsRequestBuilder
{
    private string _businessName = "Test Business";
    private string _businessEmail = $"business-{Guid.NewGuid():N}@example.com";
    private string _businessPhone = "+1234567890";
    private string _country = "TestCountry";
    private string _city = "TestCity";
    private string _street = "Test Street";
    private int _houseNumber = 123;
    private int? _flatNumber = null;

    public BusinessDetailsRequestBuilder WithBusinessEmail(string email)
    {
        _businessEmail = email;
        return this;
    }

    // Compatibility alias for older tests
    public BusinessDetailsRequestBuilder WithEmail(string email) => WithBusinessEmail(email);

    public BusinessDetailsRequestBuilder WithBusinessName(string name)
    {
        _businessName = name;
        return this;
    }

    public BusinessDetailsRequestBuilder WithBusinessPhone(string phone)
    {
        _businessPhone = phone;
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

    public BusinessDetailsRequestBuilder WithOwnerName(string ownerName)
    {
        // OwnerName isn't stored on the builder currently; map to BusinessName if needed
        _businessName = ownerName;
        return this;
    }

    public BusinessDetailsRequest Build()
    {
        return new BusinessDetailsRequest
        {
            BusinessName = _businessName,
            BusinessEmail = _businessEmail,
            Email = _businessEmail,
            BusinessPhone = _businessPhone,
            OwnerName = _businessName,
            Country = _country,
            City = _city,
            Street = _street,
            HouseNumber = _houseNumber,
            FlatNumber = _flatNumber
        };
    }
}

/// <summary>
/// Builder for CartDiscountRequest - provides fluent API for creating test cart discount data.
/// </summary>
public sealed class CartDiscountRequestBuilder
{
    private int _value = 10;
    private bool _isPercentage = true;
    private DateTime? _endDate = DateTime.UtcNow.AddDays(30);

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

    public CartDiscountRequestBuilder WithEndDate(DateTime? endDate)
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

/// <summary>
/// Builder for CashRequest - provides fluent API for creating test cash transaction data.
/// </summary>
public sealed class CashRequestBuilder
{
    private int _cartId = 1;
    private ulong _amount = 10000;
    private int? _tip = null;
    private string _transactionRef = $"txn-{Guid.NewGuid():N}";
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
