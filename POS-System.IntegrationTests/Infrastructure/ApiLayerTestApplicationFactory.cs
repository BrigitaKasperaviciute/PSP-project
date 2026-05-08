using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using POS_System.Business.Dtos;
using POS_System.Api;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Business.Services.Interfaces;
using POS_System.Business.Utils;
using POS_System.Common.Exceptions;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace POS_System.IntegrationTests.Infrastructure;

public sealed class ApiLayerTestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _configuration = new()
    {
        ["ConnectionStrings:LocalConnection"] = "Host=localhost;Database=pos_tests;Username=pos;Password=pos",
        ["POSJwtSecretKey"] = "integration-tests-key-1234567890",
        ["POSIssuer"] = "https://integration-tests.local",
        ["POSAudience"] = "https://integration-tests.local",
        ["Stripe:SecretKey"] = "sk_test_fake",
        ["Stripe:PublicKey"] = "pk_test_fake",
        ["EmailConfiguration:From"] = "tests@example.com",
        ["EmailConfiguration:SmtpServer"] = "localhost",
        ["EmailConfiguration:Port"] = "25",
        ["EmailConfiguration:UserName"] = "tests",
        ["EmailConfiguration:Password"] = "tests",
        ["FileProvider:Events:Path"] = "Logs/events.log",
        ["FileProvider:Events:FileCreationInterval"] = "12:00:00",
        ["FileProvider:Exceptions:Path"] = "Logs/exceptions.log",
        ["FileProvider:Exceptions:FileCreationInterval"] = "24:00:00",
        ["BusinessDetails:FileName"] = "business-details.integration.json",
        ["BusinessDetails:RelativePath"] = "./"
    };

    public Mock<IAuthService> AuthServiceMock { get; } = new(MockBehavior.Loose);
    public Mock<IBusinessDetailService> BusinessDetailServiceMock { get; } = new(MockBehavior.Loose);
    public Mock<ICartService> CartServiceMock { get; } = new(MockBehavior.Loose);
    public Mock<ICartDiscountService> CartDiscountServiceMock { get; } = new(MockBehavior.Loose);
    public Mock<ICartItemService> CartItemServiceMock { get; } = new(MockBehavior.Loose);
    public Mock<IEmployeeeService> EmployeeServiceMock { get; } = new(MockBehavior.Loose);
    public Mock<IGiftCardService> GiftCardServiceMock { get; } = new(MockBehavior.Loose);
    public Mock<IItemDiscountService> ItemDiscountServiceMock { get; } = new(MockBehavior.Loose);
    public Mock<IPaymentService> PaymentServiceMock { get; } = new(MockBehavior.Loose);
    public Mock<IProductService> ProductServiceMock { get; } = new(MockBehavior.Loose);
    public Mock<IProductModificationService> ProductModificationServiceMock { get; } = new(MockBehavior.Loose);
    public Mock<IServiceOfService> ServiceServiceMock { get; } = new(MockBehavior.Loose);
    public Mock<IServiceReservationService> ServiceReservationServiceMock { get; } = new(MockBehavior.Loose);
    public Mock<ITaxService> TaxServiceMock { get; } = new(MockBehavior.Loose);
    public Mock<ITimeSlotService> TimeSlotServiceMock { get; } = new(MockBehavior.Loose);

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(_configuration);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthService>();
            services.RemoveAll<IBusinessDetailService>();
            services.RemoveAll<ICartService>();
            services.RemoveAll<ICartDiscountService>();
            services.RemoveAll<ICartItemService>();
            services.RemoveAll<IEmployeeeService>();
            services.RemoveAll<IGiftCardService>();
            services.RemoveAll<IItemDiscountService>();
            services.RemoveAll<IPaymentService>();
            services.RemoveAll<IProductService>();
            services.RemoveAll<IProductModificationService>();
            services.RemoveAll<IServiceOfService>();
            services.RemoveAll<IServiceReservationService>();
            services.RemoveAll<ITaxService>();
            services.RemoveAll<ITimeSlotService>();

            services.AddSingleton(AuthServiceMock.Object);
            services.AddSingleton(BusinessDetailServiceMock.Object);
            services.AddSingleton(CartServiceMock.Object);
            services.AddSingleton(CartDiscountServiceMock.Object);
            services.AddSingleton(CartItemServiceMock.Object);
            services.AddSingleton(EmployeeServiceMock.Object);
            services.AddSingleton(GiftCardServiceMock.Object);
            services.AddSingleton(ItemDiscountServiceMock.Object);
            services.AddSingleton(PaymentServiceMock.Object);
            services.AddSingleton(ProductServiceMock.Object);
            services.AddSingleton(ProductModificationServiceMock.Object);
            services.AddSingleton(ServiceServiceMock.Object);
            services.AddSingleton(ServiceReservationServiceMock.Object);
            services.AddSingleton(TaxServiceMock.Object);
            services.AddSingleton(TimeSlotServiceMock.Object);

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            });
        });
    }

    public void ResetMocks()
    {
        AuthServiceMock.Reset();
        BusinessDetailServiceMock.Reset();
        CartServiceMock.Reset();
        CartDiscountServiceMock.Reset();
        CartItemServiceMock.Reset();
        EmployeeServiceMock.Reset();
        GiftCardServiceMock.Reset();
        ItemDiscountServiceMock.Reset();
        PaymentServiceMock.Reset();
        ProductServiceMock.Reset();
        ProductModificationServiceMock.Reset();
        ServiceServiceMock.Reset();
        ServiceReservationServiceMock.Reset();
        TaxServiceMock.Reset();
        TimeSlotServiceMock.Reset();

        ConfigureDefaultBehavior();
    }

    private void ConfigureDefaultBehavior()
    {
        AuthServiceMock.Setup(x => x.RegisterUserAsync(It.IsAny<UserRegisterRequest>()))
            .ReturnsAsync((EmployeeResponse)null!);
        AuthServiceMock.Setup(x => x.LoginUserAsync(It.IsAny<UserLoginRequest>()))
            .ReturnsAsync((UserLoginResponse)null!);
        AuthServiceMock.Setup(x => x.ForgotPasswordAsync(It.IsAny<ForgotPasswordRequest>()))
            .ReturnsAsync((PasswordRecoveryResponse)null!);
        AuthServiceMock.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ReturnsAsync((PasswordRecoveryResponse)null!);

        BusinessDetailServiceMock.Setup(x => x.GetBusinessDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessDetailsResponse)null!);
        BusinessDetailServiceMock.Setup(x => x.CreateOrUpdateBusinessDetailsAsync(It.IsAny<BusinessDetailsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessDetailsResponse)null!);

        CartServiceMock.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((PagedResponse<CartResponse>)null!);
        CartServiceMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartResponse)null!);
        CartServiceMock.Setup(x => x.CreateCartAsync(It.IsAny<CartRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartResponse)null!);
        CartServiceMock.Setup(x => x.DeleteCartAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        CartServiceMock.Setup(x => x.UpdateCartStatusAsync(It.IsAny<int>(), It.IsAny<POS_System.Common.Enums.CartStatusEnum>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        CartServiceMock.Setup(x => x.ApplyDiscountForCartAsync(It.IsAny<int>(), It.IsAny<ApplyDiscountRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartDiscountResponse)null!);
        CartServiceMock.Setup(x => x.GetCartDiscountAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartDiscountResponse?)null);

        CartDiscountServiceMock.Setup(x => x.CreateCartDiscountAsync(It.IsAny<CartDiscountRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartDiscountResponse)null!);
        CartDiscountServiceMock.Setup(x => x.GetCartDiscountByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartDiscountResponse)null!);
        CartDiscountServiceMock.Setup(x => x.DeleteCartDiscountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        CartItemServiceMock.Setup(x => x.GetAllCartItemsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((PagedResponse<CartItemResponse>)null!);
        CartItemServiceMock.Setup(x => x.GetCartItemByIdAndCartIdAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartItemResponse?)null);
        CartItemServiceMock.Setup(x => x.CreateCartItemAsync(It.IsAny<CartItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartItemResponse)null!);
        CartItemServiceMock.Setup(x => x.UpdateCartItemAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CartItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartItemResponse)null!);
        CartItemServiceMock.Setup(x => x.DeleteCartItemAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        CartItemServiceMock.Setup(x => x.LinkCartItemToProductModificationsAsync(It.IsAny<int>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        CartItemServiceMock.Setup(x => x.UnlinkCartItemFromProductModificationsAsync(It.IsAny<int>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        EmployeeServiceMock.Setup(x => x.GetEmployeesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagedResponse<EmployeeResponse>)null!);
        EmployeeServiceMock.Setup(x => x.GetEmployeeByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeResponse)null!);
        EmployeeServiceMock.Setup(x => x.UpdateEmployeeByIdAsync(It.IsAny<int>(), It.IsAny<EmployeeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeResponse)null!);
        EmployeeServiceMock.Setup(x => x.DeleteEmployeeByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeResponse)null!);

        GiftCardServiceMock.Setup(x => x.GetAllGiftCardsAsync(It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((PagedResponse<GiftCardResponse>)null!);
        GiftCardServiceMock.Setup(x => x.GetGiftCardByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GiftCardResponse?)null);
        GiftCardServiceMock.Setup(x => x.CreateGiftCardAsync(It.IsAny<GiftCardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GiftCardResponse)null!);
        GiftCardServiceMock.Setup(x => x.UpdateGiftCardAsync(It.IsAny<string>(), It.IsAny<GiftCardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GiftCardResponse)null!);
        GiftCardServiceMock.Setup(x => x.DeleteGiftCardAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        ItemDiscountServiceMock.Setup(x => x.GetAllItemDiscountsAsync(It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((PagedResponse<ItemDiscountResponse>)null!);
        ItemDiscountServiceMock.Setup(x => x.GetItemDiscountByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ItemDiscountResponse)null!);
        ItemDiscountServiceMock.Setup(x => x.CreateItemDiscountAsync(It.IsAny<ItemDiscountRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ItemDiscountResponse)null!);
        ItemDiscountServiceMock.Setup(x => x.UpdateItemDiscountAsync(It.IsAny<int>(), It.IsAny<ItemDiscountRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ItemDiscountResponse)null!);
        ItemDiscountServiceMock.Setup(x => x.DeleteItemDiscountAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        ItemDiscountServiceMock.Setup(x => x.LinkItemDiscountToItemsAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        ItemDiscountServiceMock.Setup(x => x.UnlinkItemDiscountFromItemsAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        ItemDiscountServiceMock.Setup(x => x.GetItemDiscountsLinkedToItemId(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ItemDiscountResponse>)Array.Empty<ItemDiscountResponse>());

        PaymentServiceMock.Setup(x => x.RegisterCashTransactionAsync(It.IsAny<CashRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransactionResponse)null!);
        PaymentServiceMock.Setup(x => x.IssueRefundAsync(It.IsAny<DateTime>(), It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransactionResponse)null!);
        PaymentServiceMock.Setup(x => x.GetTransactionsByCartAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<TransactionResponse>());
        PaymentServiceMock.Setup(x => x.FullCheckoutAsync(It.IsAny<CheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckoutResponse)null!);
        PaymentServiceMock.Setup(x => x.InitializePartialCheckoutAsync(It.IsAny<InitPartialCheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PartialCheckoutResponse)null!);
        PaymentServiceMock.Setup(x => x.PartialCheckoutAsync(It.IsAny<PartialCheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckoutResponse)null!);
        PaymentServiceMock.Setup(x => x.FullCheckoutSuccessAsync(It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>()))
            .ReturnsAsync("/redirect-full");
        PaymentServiceMock.Setup(x => x.PartialCheckoutSuccessAsync(It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>()))
            .ReturnsAsync("/redirect-partial");
        PaymentServiceMock.Setup(x => x.CheckoutFailAsync(It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<long?>()))
            .ReturnsAsync("/redirect-fail");

        ProductServiceMock.Setup(x => x.GetProductsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagedResponse<ProductResponse?>)null!);
        ProductServiceMock.Setup(x => x.GetProductByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductResponse?)null);
        ProductServiceMock.Setup(x => x.GetProductVersionsByProductIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ProductResponse?>)Array.Empty<ProductResponse?>());
        ProductServiceMock.Setup(x => x.CreateProductAsync(It.IsAny<ProductRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductResponse)null!);
        ProductServiceMock.Setup(x => x.UpdateProductByIdAsync(It.IsAny<int>(), It.IsAny<ProductRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductResponse)null!);
        ProductServiceMock.Setup(x => x.DeleteProductByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductResponse)null!);
        ProductServiceMock.Setup(x => x.GetProductsLinkedToTaxId(It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ProductResponse>)Array.Empty<ProductResponse>());
        ProductServiceMock.Setup(x => x.GetProductsLinkedToItemDiscountId(It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ProductResponse>)Array.Empty<ProductResponse>());

        ProductModificationServiceMock.Setup(x => x.GetProductModificationsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagedResponse<ProductModificationResponse?>)null!);
        ProductModificationServiceMock.Setup(x => x.GetProductModificationByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductModificationResponse?)null);
        ProductModificationServiceMock.Setup(x => x.GetProductModificationVersionsByProductModificationIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ProductModificationResponse?>)Array.Empty<ProductModificationResponse?>());
        ProductModificationServiceMock.Setup(x => x.CreateProductModificationAsync(It.IsAny<ProductModificationRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductModificationResponse)null!);
        ProductModificationServiceMock.Setup(x => x.UpdateProductModificationByIdAsync(It.IsAny<int>(), It.IsAny<ProductModificationRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductModificationResponse)null!);
        ProductModificationServiceMock.Setup(x => x.DeleteProductModificationByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductModificationResponse)null!);
        ProductModificationServiceMock.Setup(x => x.GetProductModificationsLinkedToCartItemId(It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ProductModificationResponse>)Array.Empty<ProductModificationResponse>());
        ProductModificationServiceMock.Setup(x => x.GetProductModificationsLinkedToProductId(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagedResponse<ProductModificationResponse?>)null!);

        ServiceServiceMock.Setup(x => x.GetAllServicesAsync(It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((PagedResponse<ServiceResponse>)null!);
        ServiceServiceMock.Setup(x => x.GetServiceByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceResponse?)null);
        ServiceServiceMock.Setup(x => x.CreateServiceAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceResponse)null!);
        ServiceServiceMock.Setup(x => x.UpdateServiceAsync(It.IsAny<int>(), It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceResponse)null!);
        ServiceServiceMock.Setup(x => x.DeleteServiceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        ServiceServiceMock.Setup(x => x.GetServicesLinkedToTaxId(It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ServiceResponse>)Array.Empty<ServiceResponse>());
        ServiceServiceMock.Setup(x => x.GetServicesLinkedToItemDiscountId(It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ServiceResponse>)Array.Empty<ServiceResponse>());

        ServiceReservationServiceMock.Setup(x => x.GetServiceReservationsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagedResponse<ServiceReservationResponse?>)null!);
        ServiceReservationServiceMock.Setup(x => x.GetServiceReservationByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceReservationResponse?)null);
        ServiceReservationServiceMock.Setup(x => x.CreateServiceReservationAsync(It.IsAny<ServiceReservationRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceReservationResponse)null!);
        ServiceReservationServiceMock.Setup(x => x.UpdateServiceReservationByIdAsync(It.IsAny<int>(), It.IsAny<ServiceReservationRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceReservationResponse)null!);

        TaxServiceMock.Setup(x => x.GetAllTaxesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagedResponse<TaxResponse>)null!);
        TaxServiceMock.Setup(x => x.GetTaxByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaxResponse)null!);
        TaxServiceMock.Setup(x => x.CreateTaxAsync(It.IsAny<TaxRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaxResponse)null!);
        TaxServiceMock.Setup(x => x.UpdateTaxAsync(It.IsAny<int>(), It.IsAny<TaxRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaxResponse)null!);
        TaxServiceMock.Setup(x => x.DeleteTaxAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        TaxServiceMock.Setup(x => x.LinkTaxToItemsAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        TaxServiceMock.Setup(x => x.UnlinkTaxFromItemsAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        TaxServiceMock.Setup(x => x.GetTaxesLinkedToItemId(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<TaxResponse>)Array.Empty<TaxResponse>());

        TimeSlotServiceMock.Setup(x => x.GetTimeSlotsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagedResponse<TimeSlotResponse?>)null!);
        TimeSlotServiceMock.Setup(x => x.GetTimeSlotByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TimeSlotResponse?)null);
        TimeSlotServiceMock.Setup(x => x.CreateTimeSlotAsync(It.IsAny<TimeSlotRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TimeSlotResponse)null!);
        TimeSlotServiceMock.Setup(x => x.UpdateTimeSlotAsync(It.IsAny<int>(), It.IsAny<TimeSlotRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TimeSlotResponse)null!);
        TimeSlotServiceMock.Setup(x => x.DeleteTimeSlotAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TimeSlotResponse)null!);
    }
}

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("ItemRead", "Y"),
            new Claim("ItemWrite", "Y"),
            new Claim("ServiceRead", "Y"),
            new Claim("ServiceWrite", "Y"),
            new Claim("EmployeesRead", "Y"),
            new Claim("EmployeesWrite", "Y"),
            new Claim("TaxRead", "Y"),
            new Claim("TaxWrite", "Y"),
            new Claim("GiftCardRead", "Y"),
            new Claim("GiftCardWrite", "Y"),
            new Claim("CartItemRead", "Y"),
            new Claim("CartItemWrite", "Y"),
            new Claim("ItemDiscountRead", "Y"),
            new Claim("ItemDiscountWrite", "Y"),
            new Claim("BusinessDetailsRead", "Y"),
            new Claim("BusinessDetailsWrite", "Y"),
            new Claim("TransactionRead", "Y"),
            new Claim("TransactionWrite", "Y"),
            new Claim("HistoricTransactionRead", "Y"),
            new Claim("HistoricTransactionWrite", "Y"),
            new Claim("HistoricRead", "Y"),
            new Claim("HistoricWrite", "Y")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
