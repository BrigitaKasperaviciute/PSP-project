using POS_System.Business.Dtos.Request;

namespace POS_System.IntegrationTests.Infrastructure;

/// <summary>
/// Builder for creating test TaxRequest objects with fluent API.
/// </summary>
public sealed class TaxRequestBuilder
{
    private string _name = $"Tax-{Guid.NewGuid():N}";
    private int _rate = 5;
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

    public TaxRequest Build() => new()
    {
        Name = _name,
        Rate = _rate,
        IsPercentage = _isPercentage
    };
}

/// <summary>
/// Builder for creating test ProductRequest objects with fluent API.
/// </summary>
public sealed class ProductRequestBuilder
{
    private string _name = $"Product-{Guid.NewGuid():N}";
    private string _description = "Test product description";
    private int _price = 10000; // 100.00 in cents
    private string _imageURL = "https://example.com/image.jpg";
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

    public ProductRequestBuilder WithImageURL(string imageURL)
    {
        _imageURL = imageURL;
        return this;
    }

    public ProductRequestBuilder WithStock(int stock)
    {
        _stock = stock;
        return this;
    }

    public ProductRequest Build() => new()
    {
        Name = _name,
        Description = _description,
        Price = _price,
        ImageURL = _imageURL,
        Stock = _stock
    };
}

/// <summary>
/// Builder for creating test CartRequest objects with fluent API.
/// </summary>
public sealed class CartRequestBuilder
{
    private int _employeeVersionId = 1;

    public CartRequestBuilder WithEmployeeVersionId(int employeeVersionId)
    {
        _employeeVersionId = employeeVersionId;
        return this;
    }

    public CartRequest Build() => new()
    {
        EmployeeVersionId = _employeeVersionId
    };
}

/// <summary>
/// Builder for creating test ServiceRequest objects with fluent API.
/// </summary>
public sealed class ServiceRequestBuilder
{
    private string _name = $"Service-{Guid.NewGuid():N}";
    private string _description = "Test service description";
    private int _duration = 60;
    private int _price = 5000; // 50.00 in cents
    private string _imageURL = "https://example.com/image.jpg";
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

    public ServiceRequestBuilder WithImageURL(string imageURL)
    {
        _imageURL = imageURL;
        return this;
    }

    public ServiceRequestBuilder WithEmployeeId(int employeeId)
    {
        _employeeId = employeeId;
        return this;
    }

    public ServiceRequest Build() => new()
    {
        Name = _name,
        Description = _description,
        Duration = _duration,
        Price = _price,
        ImageURL = _imageURL,
        EmployeeId = _employeeId
    };
}

/// <summary>
/// Builder for creating test ItemDiscountRequest objects with fluent API.
/// </summary>
public sealed class ItemDiscountRequestBuilder
{
    private int _value = 10;
    private bool _isPercentage = true;
    private string _description = "Test discount";
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

    public ItemDiscountRequestBuilder WithStartDate(DateTime startDate)
    {
        _startDate = startDate;
        return this;
    }

    public ItemDiscountRequestBuilder WithEndDate(DateTime endDate)
    {
        _endDate = endDate;
        return this;
    }

    public ItemDiscountRequest Build() => new()
    {
        Value = _value,
        IsPercentage = _isPercentage,
        Description = _description,
        StartDate = _startDate,
        EndDate = _endDate
    };
}

/// <summary>
/// Builder for creating test GiftCardRequest objects with fluent API.
/// </summary>
public sealed class GiftCardRequestBuilder
{
    private DateOnly _date = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1));
    private int _value = 5000; // 50.00 in cents

    public GiftCardRequestBuilder WithDate(DateOnly date)
    {
        _date = date;
        return this;
    }

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

    public GiftCardRequest Build() => new()
    {
        Date = _date,
        Value = _value
    };
}

/// <summary>
/// Builder for creating test EmployeeRequest objects with fluent API.
/// </summary>
public sealed class EmployeeRequestBuilder
{
    private string _firstName = $"Employee-{Guid.NewGuid():N}";
    private string _lastName = "Test";
    private DateOnly _birthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-25));
    private string _userName = $"employee-{Guid.NewGuid():N}";
    private string _email = $"employee-{Guid.NewGuid():N}@example.com";
    private string _phoneNumber = "1234567890";
    private int _roleId = 1;

    public EmployeeRequestBuilder WithFirstName(string firstName)
    {
        _firstName = firstName;
        return this;
    }

    public EmployeeRequestBuilder WithName(string name)
    {
        _firstName = name;
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

    public EmployeeRequestBuilder WithSalary(int salary)
    {
        _roleId = salary;
        return this;
    }

    public EmployeeRequestBuilder WithCommission(int commission)
    {
        _roleId = commission;
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

    public EmployeeRequest Build() => new(
        _firstName,
        _lastName,
        _birthDate,
        _userName,
        _email,
        _phoneNumber,
        _roleId);
}

/// <summary>
/// Builder for creating test UserRegisterRequest objects with fluent API.
/// </summary>
public sealed class UserRegisterRequestBuilder
{
    private string _email = $"user-{Guid.NewGuid():N}@example.com";
    private string _userName = $"user-{Guid.NewGuid():N}";
    private string _firstName = "Integration";
    private string _lastName = "User";
    private string _password = "Test@12345";
    private string _phoneNumber = "123456789";
    private DateOnly _birthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-25));
    private int _roleId = 1;

    public UserRegisterRequestBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public UserRegisterRequestBuilder WithUserName(string userName)
    {
        _userName = userName;
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

    public UserRegisterRequestBuilder WithPassword(string password)
    {
        _password = password;
        return this;
    }

    public UserRegisterRequestBuilder WithPhoneNumber(string phoneNumber)
    {
        _phoneNumber = phoneNumber;
        return this;
    }

    public UserRegisterRequestBuilder WithBirthDate(DateOnly birthDate)
    {
        _birthDate = birthDate;
        return this;
    }

    public UserRegisterRequestBuilder WithRoleId(int roleId)
    {
        _roleId = roleId;
        return this;
    }

    public UserRegisterRequest Build() => new(
        _email,
        _userName,
        _firstName,
        _lastName,
        _password,
        _phoneNumber,
        _birthDate,
        _roleId);
}

/// <summary>
/// Builder for creating test CashRequest objects with fluent API.
/// </summary>
public sealed class CashRequestBuilder
{
    private int _cartId = 1;
    private ulong _amount = 5000;
    private int? _tip;
    private string _transactionRef = $"ref-{Guid.NewGuid():N}";
    private string? _phoneNumber;

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

    public CashRequest Build() => new(
        _cartId,
        _amount,
        _tip,
        _transactionRef,
        _phoneNumber);
}

/// <summary>
/// Builder for creating test RefundRequest objects with fluent API.
/// </summary>
public sealed class RefundRequestBuilder
{
    private int _cartId = 1;
    private bool _isCard;

    public RefundRequestBuilder WithCartId(int cartId)
    {
        _cartId = cartId;
        return this;
    }

    public RefundRequestBuilder WithIsCard(bool isCard)
    {
        _isCard = isCard;
        return this;
    }

    public RefundRequest Build() => new(_cartId, _isCard);
}

/// <summary>
/// Builder for creating test CartDiscountRequest objects with fluent API.
/// </summary>
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

    public CartDiscountRequest Build() => new()
    {
        Value = _value,
        IsPercentage = _isPercentage,
        EndDate = _endDate
    };
}
