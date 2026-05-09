using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using POS_System.Api.Controllers;
using POS_System.Business.Services.Interfaces;
using Xunit;

namespace POS_System.IntegrationTests;

public sealed class ApiLayerCoverageScenariosTests
{
    [Fact]
    public async Task ApiScenario_ContainsHappyAndNegativeFlow()
    {
        foreach (var scenario in ApiScenarios)
        {
            var happyResult = await scenario.ExecuteAsync(false);
            AssertResultKind(happyResult, scenario.ExpectedHappyResultKind);

            var exception = await Record.ExceptionAsync(() => scenario.ExecuteAsync(true));
            var root = Unwrap(exception);

            Assert.IsType<InvalidOperationException>(root);
        }
    }

    public static IReadOnlyList<ControllerScenario> ApiScenarios =>
        new List<ControllerScenario>
        {
            // AuthController
            new("Auth.Register", shouldThrow => CreateController<AuthController, IAuthService>(shouldThrow).RegisterUserAsync(null!), ResultKind.OkObject),
            new("Auth.Login", shouldThrow => CreateController<AuthController, IAuthService>(shouldThrow).LoginUserAsync(null!), ResultKind.OkObject),
            new("Auth.ForgotPassword", shouldThrow => CreateController<AuthController, IAuthService>(shouldThrow).ForgotPasswordAsync(null!), ResultKind.OkObject),
            new("Auth.ResetPassword", shouldThrow => CreateController<AuthController, IAuthService>(shouldThrow).ResetPasswordAsync(null!), ResultKind.OkObject),

            // EmployeeController
            new("Employee.GetEmployees", shouldThrow => CreateController<EmployeeController, IEmployeeeService>(shouldThrow).GetEmployeesAsync(null, CancellationToken.None), ResultKind.OkObject),
            new("Employee.GetById", shouldThrow => CreateController<EmployeeController, IEmployeeeService>(shouldThrow).GetEmployeeByIdAsync(1, CancellationToken.None), ResultKind.OkObject),
            new("Employee.Update", shouldThrow => CreateController<EmployeeController, IEmployeeeService>(shouldThrow).UpdateEmployeeByIdAsync(1, null!, CancellationToken.None), ResultKind.OkObject),
            new("Employee.Delete", shouldThrow => CreateController<EmployeeController, IEmployeeeService>(shouldThrow).DeleteEmployeeByIdAsync(1, CancellationToken.None), ResultKind.OkObject),

            // BusinessDetailController
            new("BusinessDetail.Get", shouldThrow => CreateController<BusinessDetailController, IBusinessDetailService>(shouldThrow).GetBusinessDetails(CancellationToken.None), ResultKind.OkObject),
            new("BusinessDetail.Create", shouldThrow => CreateController<BusinessDetailController, IBusinessDetailService>(shouldThrow).CreateBusinessDetails(null!, CancellationToken.None), ResultKind.OkObject),
            new("BusinessDetail.Update", shouldThrow => CreateController<BusinessDetailController, IBusinessDetailService>(shouldThrow).UpdateBusinessDetails(null!, CancellationToken.None), ResultKind.OkObject),

            // CartController
            new("Cart.GetAll", shouldThrow => CreateController<CartController, ICartService>(shouldThrow).GetAll(CancellationToken.None), ResultKind.OkObject),
            new("Cart.GetById", shouldThrow => CreateController<CartController, ICartService>(shouldThrow).GetByID(1, CancellationToken.None), ResultKind.OkObject),
            new("Cart.Create", shouldThrow => CreateController<CartController, ICartService>(shouldThrow).Create(null!, CancellationToken.None), ResultKind.OkObject),
            new("Cart.Delete", shouldThrow => CreateController<CartController, ICartService>(shouldThrow).Delete(1, CancellationToken.None), ResultKind.Ok),
            new("Cart.ApplyDiscount", shouldThrow => CreateController<CartController, ICartService>(shouldThrow).ApplyDiscountToCart(1, null!, CancellationToken.None), ResultKind.OkObject),
            new("Cart.GetDiscount", shouldThrow => CreateController<CartController, ICartService>(shouldThrow).GetCartDiscountAsync(1, CancellationToken.None), ResultKind.OkObject),

            // CartDiscountController
            new("CartDiscount.Create", shouldThrow => CreateController<CartDiscountController, ICartDiscountService>(shouldThrow).CreateCartDiscount(null!, CancellationToken.None), ResultKind.OkObject),
            new("CartDiscount.GetById", shouldThrow => CreateController<CartDiscountController, ICartDiscountService>(shouldThrow).GetCartDiscountById("cart-disc", CancellationToken.None), ResultKind.OkObject),
            new("CartDiscount.DeleteById", shouldThrow => CreateController<CartDiscountController, ICartDiscountService>(shouldThrow).DeleteCartDiscountById("cart-disc", CancellationToken.None), ResultKind.Ok),

            // CartItemController
            new("CartItem.GetAll", shouldThrow => CreateController<CartItemController, ICartItemService>(shouldThrow).GetAllCartItems(1, CancellationToken.None), ResultKind.OkObject),
            new("CartItem.GetById", shouldThrow => CreateController<CartItemController, ICartItemService>(shouldThrow).GetCartItemByIdAndCartId(1, 2, CancellationToken.None), ResultKind.OkObject),
            new("CartItem.Create", shouldThrow => CreateController<CartItemController, ICartItemService>(shouldThrow).CreateCartItem(null!, CancellationToken.None), ResultKind.OkObject),
            new("CartItem.Update", shouldThrow => CreateController<CartItemController, ICartItemService>(shouldThrow).UpdateCartItem(1, 2, null!, CancellationToken.None), ResultKind.OkObject),
            new("CartItem.Delete", shouldThrow => CreateController<CartItemController, ICartItemService>(shouldThrow).DeleteCartItem(1, 2, CancellationToken.None), ResultKind.NoContent),
            new("CartItem.LinkModifications", shouldThrow => CreateController<CartItemController, ICartItemService>(shouldThrow).LinkCartItemToProductModifications(1, new[] { 1, 2 }, CancellationToken.None), ResultKind.Ok),
            new("CartItem.UnlinkModifications", shouldThrow => CreateController<CartItemController, ICartItemService>(shouldThrow).UnlinkCartItemFromProductModifications(1, new[] { 1 }, CancellationToken.None), ResultKind.Ok),

            // GiftCardController
            new("GiftCard.GetAll", shouldThrow => CreateController<GiftCardController, IGiftCardService>(shouldThrow).GetAllGiftCards(CancellationToken.None), ResultKind.OkObject),
            new("GiftCard.GetById", shouldThrow => CreateController<GiftCardController, IGiftCardService>(shouldThrow).GetGiftCardById("gift-1", CancellationToken.None), ResultKind.OkObject),
            new("GiftCard.Create", shouldThrow => CreateController<GiftCardController, IGiftCardService>(shouldThrow).CreateGiftCard(null!, CancellationToken.None), ResultKind.OkObject),
            new("GiftCard.Update", shouldThrow => CreateController<GiftCardController, IGiftCardService>(shouldThrow).UpdateGiftCard("gift-1", null!, CancellationToken.None), ResultKind.OkObject),
            new("GiftCard.Delete", shouldThrow => CreateController<GiftCardController, IGiftCardService>(shouldThrow).DeleteGiftCard("gift-1", CancellationToken.None), ResultKind.NoContent),

            // ItemDiscountController
            new("ItemDiscount.GetAll", shouldThrow => CreateController<ItemDiscountController, IItemDiscountService>(shouldThrow).GetAllItemDiscounts(CancellationToken.None), ResultKind.OkObject),
            new("ItemDiscount.Create", shouldThrow => CreateController<ItemDiscountController, IItemDiscountService>(shouldThrow).CreateItemDiscount(null!, CancellationToken.None), ResultKind.OkObject),
            new("ItemDiscount.GetById", shouldThrow => CreateController<ItemDiscountController, IItemDiscountService>(shouldThrow).GetItemDiscountById(1, CancellationToken.None), ResultKind.OkObject),
            new("ItemDiscount.DeleteById", shouldThrow => CreateController<ItemDiscountController, IItemDiscountService>(shouldThrow).DeleteItemDiscountById(1, CancellationToken.None), ResultKind.Ok),
            new("ItemDiscount.UpdateById", shouldThrow => CreateController<ItemDiscountController, IItemDiscountService>(shouldThrow).UpdateItemDiscountById(1, null!, CancellationToken.None), ResultKind.OkObject),
            new("ItemDiscount.LinkItems", shouldThrow => CreateController<ItemDiscountController, IItemDiscountService>(shouldThrow).LinkItemDiscountToItems(1, true, new[] { 1, 2 }, CancellationToken.None), ResultKind.Ok),
            new("ItemDiscount.UnlinkItems", shouldThrow => CreateController<ItemDiscountController, IItemDiscountService>(shouldThrow).UnlinkItemDiscountFromItems(1, false, new[] { 1, 2 }, CancellationToken.None), ResultKind.Ok),
            new("ItemDiscount.GetLinkedToItem", shouldThrow => CreateController<ItemDiscountController, IItemDiscountService>(shouldThrow).GetItemDiscountsLinkedToItemId(1, true, null, CancellationToken.None), ResultKind.OkObject),

            // PaymentController
            new("Payment.RegisterCash", shouldThrow => CreateController<PaymentController, IPaymentService>(shouldThrow).RegisterCashTransactionAsync(null!, CancellationToken.None), ResultKind.OkObject),
            new("Payment.IssueRefund", shouldThrow => CreateController<PaymentController, IPaymentService>(shouldThrow).IssueRefundAsync(DateTime.UtcNow, null!, CancellationToken.None), ResultKind.OkObject),
            new("Payment.GetTransactionsByCart", shouldThrow => CreateController<PaymentController, IPaymentService>(shouldThrow).GetTransactionsByCartAsync(1), ResultKind.OkObject),
            new("Payment.FullCheckout", shouldThrow => CreateController<PaymentController, IPaymentService>(shouldThrow).FullCheckoutAsync(null!, CancellationToken.None), ResultKind.OkObject),
            new("Payment.InitializePartialCheckout", shouldThrow => CreateController<PaymentController, IPaymentService>(shouldThrow).InitializePartialCheckoutAsync(null!, CancellationToken.None), ResultKind.OkObject),
            new("Payment.PartialCheckout", shouldThrow => CreateController<PaymentController, IPaymentService>(shouldThrow).PartialCheckoutAsync(null!, CancellationToken.None), ResultKind.OkObject),
            new("Payment.FullCheckoutSuccess", shouldThrow => CreateController<PaymentController, IPaymentService>(shouldThrow).FullCheckoutSuccessAsync(DateTime.UtcNow, 1, "session", null), ResultKind.Redirect),
            new("Payment.PartialCheckoutSuccess", shouldThrow => CreateController<PaymentController, IPaymentService>(shouldThrow).PartialCheckoutSuccessAsync(DateTime.UtcNow, 1, "session", null), ResultKind.Redirect),
            new("Payment.CheckoutFail", shouldThrow => CreateController<PaymentController, IPaymentService>(shouldThrow).CheckoutFailAsync(DateTime.UtcNow, 1, "session", null, null), ResultKind.Redirect),

            // ProductController
            new("Product.GetAll", shouldThrow => CreateController<ProductController, IProductService>(shouldThrow).GetAllProducts(null, CancellationToken.None), ResultKind.OkObject),
            new("Product.GetById", shouldThrow => CreateController<ProductController, IProductService>(shouldThrow).GetProductById(1, CancellationToken.None), ResultKind.OkObject),
            new("Product.GetVersionsByProductId", shouldThrow => CreateController<ProductController, IProductService>(shouldThrow).GetProductVersionsByProductId(1, CancellationToken.None), ResultKind.OkObject),
            new("Product.Create", shouldThrow => CreateController<ProductController, IProductService>(shouldThrow).CreateProduct(null, CancellationToken.None), ResultKind.OkObject),
            new("Product.Update", shouldThrow => CreateController<ProductController, IProductService>(shouldThrow).UpdateProductByProductId(1, null, CancellationToken.None), ResultKind.OkObject),
            new("Product.Delete", shouldThrow => CreateController<ProductController, IProductService>(shouldThrow).DeleteProductById(1, CancellationToken.None), ResultKind.OkObject),
            new("Product.GetLinkedToTax", shouldThrow => CreateController<ProductController, IProductService>(shouldThrow).GetProductsLinkedToTaxId(1, null, CancellationToken.None), ResultKind.OkObject),
            new("Product.GetLinkedToItemDiscount", shouldThrow => CreateController<ProductController, IProductService>(shouldThrow).GetProductsLinkedToItemDiscountId(1, null, CancellationToken.None), ResultKind.OkObject),

            // ProductModificationController
            new("ProductModification.GetAll", shouldThrow => CreateController<ProductModificationController, IProductModificationService>(shouldThrow).GetAllProductModifications(null, CancellationToken.None), ResultKind.OkObject),
            new("ProductModification.GetById", shouldThrow => CreateController<ProductModificationController, IProductModificationService>(shouldThrow).GetProductModificationById(1, CancellationToken.None), ResultKind.OkObject),
            new("ProductModification.GetVersionsById", shouldThrow => CreateController<ProductModificationController, IProductModificationService>(shouldThrow).GetProductModificationVersionsByProductModificationId(1, CancellationToken.None), ResultKind.OkObject),
            new("ProductModification.Create", shouldThrow => CreateController<ProductModificationController, IProductModificationService>(shouldThrow).CreateProductModification(null, CancellationToken.None), ResultKind.OkObject),
            new("ProductModification.Update", shouldThrow => CreateController<ProductModificationController, IProductModificationService>(shouldThrow).UpdateProductModification(1, null, CancellationToken.None), ResultKind.OkObject),
            new("ProductModification.Delete", shouldThrow => CreateController<ProductModificationController, IProductModificationService>(shouldThrow).DeleteProductModification(1, CancellationToken.None), ResultKind.OkObject),
            new("ProductModification.GetLinkedToCartItem", shouldThrow => CreateController<ProductModificationController, IProductModificationService>(shouldThrow).GetProductModificationsLinkedToCartItemId(1, null, CancellationToken.None), ResultKind.OkObject),
            new("ProductModification.GetLinkedToProduct", shouldThrow => CreateController<ProductModificationController, IProductModificationService>(shouldThrow).GetProductModificationsLinkedToProductId(1, CancellationToken.None), ResultKind.OkObject),

            // ServiceController
            new("Service.GetAll", shouldThrow => CreateController<ServiceController, IServiceOfService>(shouldThrow).GetAllServices(CancellationToken.None), ResultKind.OkObject),
            new("Service.GetById", shouldThrow => CreateController<ServiceController, IServiceOfService>(shouldThrow).GetServiceById(1, CancellationToken.None), ResultKind.OkObject),
            new("Service.Create", shouldThrow => CreateController<ServiceController, IServiceOfService>(shouldThrow).CreateService(null!, CancellationToken.None), ResultKind.OkObject),
            new("Service.Update", shouldThrow => CreateController<ServiceController, IServiceOfService>(shouldThrow).UpdateService(1, null!, CancellationToken.None), ResultKind.OkObject),
            new("Service.Delete", shouldThrow => CreateController<ServiceController, IServiceOfService>(shouldThrow).DeleteService(1, CancellationToken.None), ResultKind.NoContent),
            new("Service.GetLinkedToTax", shouldThrow => CreateController<ServiceController, IServiceOfService>(shouldThrow).GetServicesLinkedToTaxId(1, null, CancellationToken.None), ResultKind.OkObject),
            new("Service.GetLinkedToItemDiscount", shouldThrow => CreateController<ServiceController, IServiceOfService>(shouldThrow).GetServicesLinkedToItemDiscountId(1, null, CancellationToken.None), ResultKind.OkObject),

            // ServiceReservationController
            new("ServiceReservation.GetAll", shouldThrow => CreateController<ServiceReservationController, IServiceReservationService>(shouldThrow).GetAllServiceReservations(CancellationToken.None), ResultKind.OkObject),
            new("ServiceReservation.GetById", shouldThrow => CreateController<ServiceReservationController, IServiceReservationService>(shouldThrow).GetServiceReservationById(1, CancellationToken.None), ResultKind.OkObject),
            new("ServiceReservation.Create", shouldThrow => CreateController<ServiceReservationController, IServiceReservationService>(shouldThrow).CreateServiceReservation(null, CancellationToken.None), ResultKind.OkObject),
            new("ServiceReservation.Update", shouldThrow => CreateController<ServiceReservationController, IServiceReservationService>(shouldThrow).UpdateServiceReservation(1, null, CancellationToken.None), ResultKind.OkObject),

            // TaxController
            new("Tax.GetAll", shouldThrow => CreateController<TaxController, ITaxService>(shouldThrow).GetAllTaxes(CancellationToken.None), ResultKind.OkObject),
            new("Tax.Create", shouldThrow => CreateController<TaxController, ITaxService>(shouldThrow).CreateTax(null!, CancellationToken.None), ResultKind.OkObject),
            new("Tax.GetById", shouldThrow => CreateController<TaxController, ITaxService>(shouldThrow).GetTaxById(1, CancellationToken.None), ResultKind.OkObject),
            new("Tax.DeleteById", shouldThrow => CreateController<TaxController, ITaxService>(shouldThrow).DeleteTaxById(1, CancellationToken.None), ResultKind.Ok),
            new("Tax.UpdateById", shouldThrow => CreateController<TaxController, ITaxService>(shouldThrow).UpdateTaxById(1, null!, CancellationToken.None), ResultKind.OkObject),
            new("Tax.LinkItems", shouldThrow => CreateController<TaxController, ITaxService>(shouldThrow).LinkTaxToItems(1, true, new[] { 1, 2 }, CancellationToken.None), ResultKind.Ok),
            new("Tax.UnlinkItems", shouldThrow => CreateController<TaxController, ITaxService>(shouldThrow).UnlinkTaxFromItems(1, false, new[] { 1, 2 }, CancellationToken.None), ResultKind.Ok),
            new("Tax.GetLinkedToItem", shouldThrow => CreateController<TaxController, ITaxService>(shouldThrow).GetTaxesLinkedToItemId(1, true, null, CancellationToken.None), ResultKind.OkObject),

            // TimeSlotController
            new("TimeSlot.GetAll", shouldThrow => CreateController<TimeSlotController, ITimeSlotService>(shouldThrow).GetAllTimeSlots(null, CancellationToken.None), ResultKind.OkObject),
            new("TimeSlot.GetById", shouldThrow => CreateController<TimeSlotController, ITimeSlotService>(shouldThrow).GetTimeSlotById(1, CancellationToken.None), ResultKind.OkObject),
            new("TimeSlot.Create", shouldThrow => CreateController<TimeSlotController, ITimeSlotService>(shouldThrow).CreateTimeSlot(null, CancellationToken.None), ResultKind.OkObject),
            new("TimeSlot.Update", shouldThrow => CreateController<TimeSlotController, ITimeSlotService>(shouldThrow).UpdateTimeSlot(1, null, CancellationToken.None), ResultKind.OkObject),
            new("TimeSlot.Delete", shouldThrow => CreateController<TimeSlotController, ITimeSlotService>(shouldThrow).DeleteTimeSlot(1, CancellationToken.None), ResultKind.OkObject)
        };

    private static TController CreateController<TController, TService>(bool shouldThrow)
        where TController : ControllerBase
        where TService : class
    {
        var service = ServiceProxyFactory.Create<TService>(shouldThrow);
        return (TController)Activator.CreateInstance(typeof(TController), service)!;
    }

    private static void AssertResultKind(IActionResult result, ResultKind resultKind)
    {
        switch (resultKind)
        {
            case ResultKind.OkObject:
                Assert.IsType<OkObjectResult>(result);
                break;
            case ResultKind.Ok:
                Assert.IsType<OkResult>(result);
                break;
            case ResultKind.NoContent:
                Assert.IsType<NoContentResult>(result);
                break;
            case ResultKind.Redirect:
                Assert.IsType<RedirectResult>(result);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(resultKind), resultKind, null);
        }
    }

    private static Exception Unwrap(Exception? exception)
    {
        Assert.NotNull(exception);
        var current = exception!;

        while (current is TargetInvocationException tie && tie.InnerException is not null)
        {
            current = tie.InnerException;
        }

        return current;
    }
}

public sealed record ControllerScenario(string Name, Func<bool, Task<IActionResult>> ExecuteAsync, ResultKind ExpectedHappyResultKind);

public enum ResultKind
{
    OkObject,
    Ok,
    NoContent,
    Redirect
}

internal static class ServiceProxyFactory
{
    public static T Create<T>(bool shouldThrow) where T : class
    {
        var proxy = DispatchProxy.Create<T, ServiceProxy>();
        ((ServiceProxy)(object)proxy).ShouldThrow = shouldThrow;
        return proxy;
    }

    private class ServiceProxy : DispatchProxy
    {
        public bool ShouldThrow { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
            {
                throw new InvalidOperationException("Target method was not provided.");
            }

            if (ShouldThrow)
            {
                throw new InvalidOperationException("Simulated negative flow from service.");
            }

            return BuildReturnValue(targetMethod.ReturnType);
        }

        private static object? BuildReturnValue(Type returnType)
        {
            if (returnType == typeof(Task))
            {
                return Task.CompletedTask;
            }

            if (returnType == typeof(ValueTask))
            {
                return ValueTask.CompletedTask;
            }

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = returnType.GetGenericArguments()[0];
                var value = BuildDefaultValue(resultType);
                var fromResultMethod = typeof(Task)
                    .GetMethod(nameof(Task.FromResult), BindingFlags.Public | BindingFlags.Static)!
                    .MakeGenericMethod(resultType);

                return fromResultMethod.Invoke(null, new[] { value });
            }

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                var resultType = returnType.GetGenericArguments()[0];
                var value = BuildDefaultValue(resultType);
                return Activator.CreateInstance(returnType, value);
            }

            return BuildDefaultValue(returnType);
        }

        private static object? BuildDefaultValue(Type type)
        {
            if (type == typeof(string))
            {
                return "/integration-tests";
            }

            if (type.IsArray)
            {
                return Array.CreateInstance(type.GetElementType()!, 0);
            }

            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            if (type.IsGenericType)
            {
                var genericTypeDefinition = type.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(IEnumerable<>)
                    || genericTypeDefinition == typeof(ICollection<>)
                    || genericTypeDefinition == typeof(IList<>)
                    || genericTypeDefinition == typeof(IReadOnlyCollection<>)
                    || genericTypeDefinition == typeof(IReadOnlyList<>))
                {
                    var elementType = type.GetGenericArguments()[0];
                    return Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
                }
            }

            try
            {
                return Activator.CreateInstance(type);
            }
            catch
            {
                return null;
            }
        }
    }
}
