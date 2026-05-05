using AutoMapper;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Business.Services.Interfaces;
using POS_System.Common.Constants;
using POS_System.Common.Exceptions;
using POS_System.Data.Repositories.Interfaces;
using POS_System.Domain.Entities;

namespace POS_System.Business.Services
{
    public class CartDiscountService(IUnitOfWork _unitOfWork, IMapper _mapper) : ICartDiscountService
    {
        public async Task<CartDiscountResponse> CreateCartDiscountAsync(CartDiscountRequest cartDiscountDto, CancellationToken cancellationToken)
        {
            var id = Guid.NewGuid().ToString("N");
            var cartDiscount = new CartDiscount { Id = id, IsPercentage = cartDiscountDto.IsPercentage, Value = cartDiscountDto.Value };

            await _unitOfWork.CartDiscountRepository.CreateAsync(cartDiscount, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new CartDiscountResponse
            {
                Id = id,
                Value = cartDiscountDto.Value,
                IsPercentage = cartDiscountDto.IsPercentage,
            };
        }

        public async Task DeleteCartDiscountAsync(string id, CancellationToken cancellationToken)
        {
            var cartDiscount = await _unitOfWork.CartDiscountRepository.GetByIdStringAsync(id, cancellationToken)
                ?? throw new NotFoundException(ApplicationMessages.NOT_FOUND_ERROR);

            _unitOfWork.CartDiscountRepository.Delete(cartDiscount);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
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