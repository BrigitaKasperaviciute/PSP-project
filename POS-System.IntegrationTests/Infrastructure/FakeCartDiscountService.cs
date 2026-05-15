using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Business.Services.Interfaces;
using POS_System.Common.Exceptions;
using POS_System.Common.Constants;

namespace POS_System.IntegrationTests.Infrastructure;

internal sealed class FakeCartDiscountService : ICartDiscountService
{
    private readonly Dictionary<string, CartDiscountResponse> _store = new();

    public Task<CartDiscountResponse> CreateCartDiscountAsync(CartDiscountRequest cartDiscountDto, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid().ToString("N");
        var response = new CartDiscountResponse
        {
            Id = id,
            Value = cartDiscountDto.Value,
            IsPercentage = cartDiscountDto.IsPercentage
        };
        _store[id] = response;
        return Task.FromResult(response);
    }

    public Task DeleteCartDiscountAsync(string id, CancellationToken cancellationToken)
    {
        if (!_store.Remove(id))
            throw new NotFoundException(ApplicationMessages.NOT_FOUND_ERROR);
        return Task.CompletedTask;
    }

    public Task<CartDiscountResponse> GetCartDiscountByIdAsync(string id, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(id, out var response))
            return Task.FromResult(response);
        throw new NotFoundException(ApplicationMessages.NOT_FOUND_ERROR);
    }
}
