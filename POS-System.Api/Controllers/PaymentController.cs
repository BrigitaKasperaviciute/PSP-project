using Microsoft.AspNetCore.Mvc;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Services.Interfaces;

namespace POS_System.Api.Controllers
{
    [Route("api/payments")]
    [ApiController]
    public class PaymentController(IPaymentService paymentService, Microsoft.Extensions.Hosting.IHostEnvironment env) : ControllerBase
    {
        [HttpPost("cash")]
        public async Task<IActionResult> RegisterCashTransactionAsync([FromBody] CashRequest cashRequest, CancellationToken token)
        {
            var response = await paymentService.RegisterCashTransactionAsync(cashRequest, token);

            return Ok(response);
        }

        [HttpPatch("refund/{id}")]
        public async Task<IActionResult> IssueRefundAsync([FromRoute] DateTime id, [FromBody] RefundRequest refundRequest, CancellationToken token)
        {
            var response = await paymentService.IssueRefundAsync(id, refundRequest, token);

            return Ok(response);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetTransactionsByCartAsync([FromRoute] int id)
        {
            var response = await paymentService.GetTransactionsByCartAsync(id);

            return Ok(response);
        }

        [HttpPost("full-checkout")]
        public async Task<IActionResult> FullCheckoutAsync([FromBody] CheckoutRequest checkoutRequest, CancellationToken token = default)
        {
            var response = await paymentService.FullCheckoutAsync(checkoutRequest, token);

            return Ok(response);
        }

        [HttpPost("init-partial-checkout")]
        public async Task<IActionResult> InitializePartialCheckoutAsync([FromBody] InitPartialCheckoutRequest checkoutRequest, CancellationToken token = default)
        {
            var response = await paymentService.InitializePartialCheckoutAsync(checkoutRequest, token);

            return Ok(response);
        }

        [HttpPost("partial-checkout")]
        public async Task<IActionResult> PartialCheckoutAsync([FromBody] PartialCheckoutRequest checkoutRequest, CancellationToken token = default)
        {
            var response = await paymentService.PartialCheckoutAsync(checkoutRequest, token);

            return Ok(response);
        }

        [HttpGet("full-checkout-success")]
        public async Task<IActionResult> FullCheckoutSuccessAsync([FromQuery] string transactionDate, [FromQuery] int cartId, [FromQuery] string sessionId, [FromQuery] string? phoneNumber)
        {
            DateTime.TryParse(transactionDate, out var txDate);
            var path = await paymentService.FullCheckoutSuccessAsync(txDate, sessionId, cartId, phoneNumber);

            // In integration tests we prefer returning OK instead of following external redirects
            if (env.EnvironmentName == "IntegrationTests")
                return Ok();

            return Redirect(path);
        }

        [HttpGet("partial-checkout-success")]
        public async Task<IActionResult> PartialCheckoutSuccessAsync([FromQuery] string transactionDate, [FromQuery] int cartId, [FromQuery] string sessionId, [FromQuery] string? phoneNumber)
        {
            DateTime.TryParse(transactionDate, out var txDate);
            var path = await paymentService.PartialCheckoutSuccessAsync(txDate, sessionId, cartId, phoneNumber);

            if (env.EnvironmentName == "IntegrationTests")
                return Ok();

            return Redirect(path);
        }

        [HttpGet("checkout-fail")]
        public async Task<IActionResult> CheckoutFailAsync([FromQuery] string transactionDate, [FromQuery] int cartId, [FromQuery] string sessionId, [FromQuery] string? giftCardCode, [FromQuery] long? discount)
        {
            DateTime.TryParse(transactionDate, out var txDate);
            var path = await paymentService.CheckoutFailAsync(txDate, sessionId, cartId, giftCardCode, discount);

            if (env.EnvironmentName == "IntegrationTests")
                return Ok();

            return Redirect(path);
        }
    }
}