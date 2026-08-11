using LegacyOrderApi.Dtos;

namespace LegacyOrderApi.Services
{
    /// <summary>
    /// All order business rules live behind this interface: customer
    /// eligibility checks, stock/quantity handling, discounts, tax, and
    /// total calculation. The controller only calls into this.
    /// </summary>
    public interface IOrderService
    {
        Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken);

        Task<OrderResponse?> GetOrderByIdAsync(int orderId, CancellationToken cancellationToken);
    }
}
