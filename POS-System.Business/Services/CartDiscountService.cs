using AutoMapper;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Business.Services.Interfaces;
using POS_System.Common.Constants;
using POS_System.Common.Exceptions;
using POS_System.Data.Repositories.Interfaces;
using POS_System.Domain.Entities;
using Stripe;

namespace POS_System.Business.Services
{
    public class CartDiscountService(IUnitOfWork _unitOfWork, IMapper _mapper, Microsoft.Extensions.Hosting.IHostEnvironment _env) : ICartDiscountService
    {
        public async Task<CartDiscountResponse> CreateCartDiscountAsync(CartDiscountRequest cartDiscountDto, CancellationToken cancellationToken)
        {
            // In integration tests we avoid external Stripe calls and simulate a coupon
            if (_env.EnvironmentName == "IntegrationTests")
            {
                var fakeId = Guid.NewGuid().ToString("N");
                await _unitOfWork.CartDiscountRepository.CreateAsync(new CartDiscount() { Id = fakeId, IsPercentage = cartDiscountDto.IsPercentage, Value = cartDiscountDto.Value }, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return new CartDiscountResponse
                {
                    Id = fakeId,
                    Value = cartDiscountDto.Value,
                    IsPercentage = cartDiscountDto.IsPercentage
                };
            }

            var couponService = new CouponService();
            var options = new CouponCreateOptions()
            {
                Currency = "EUR",
                RedeemBy = cartDiscountDto.EndDate,
                Duration = "forever"
            };

            if (cartDiscountDto.IsPercentage)
                options.PercentOff = cartDiscountDto.Value;
            else
                options.AmountOff = cartDiscountDto.Value;

            var coupon = await couponService.CreateAsync(options, cancellationToken: cancellationToken)
                ?? throw new InternalServerErrorException(ApplicationMessages.INTERNAL_SERVER_ERROR);

            await _unitOfWork.CartDiscountRepository.CreateAsync(new CartDiscount() { Id = coupon.Id, IsPercentage = cartDiscountDto.IsPercentage, Value = cartDiscountDto.Value }, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new CartDiscountResponse
            {
                Id = coupon.Id,
                Value = coupon.AmountOff is null ? (int)coupon.PercentOff! : (int)coupon.AmountOff,
                IsPercentage = coupon.PercentOff is not null,
            };
        }

        public async Task DeleteCartDiscountAsync(string id, CancellationToken cancellationToken)
        {
            var cartDiscount = await _unitOfWork.CartDiscountRepository.GetByIdStringAsync(id, cancellationToken)
                ?? throw new NotFoundException(ApplicationMessages.NOT_FOUND_ERROR);

            if (_env.EnvironmentName == "IntegrationTests")
            {
                _unitOfWork.CartDiscountRepository.Delete(cartDiscount);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }

            var couponService = new CouponService();
            await couponService.DeleteAsync(cartDiscount.Id);
        }

        public async Task<CartDiscountResponse> GetCartDiscountByIdAsync(string id, CancellationToken cancellationToken)
        {
            var cartDiscount = await _unitOfWork.CartDiscountRepository.GetByIdStringAsync(id, cancellationToken)
                ?? throw new NotFoundException(ApplicationMessages.NOT_FOUND_ERROR);

            var cartDiscountDto = _mapper.Map<CartDiscountResponse>(cartDiscount);
            return cartDiscountDto;
        }
    }
}