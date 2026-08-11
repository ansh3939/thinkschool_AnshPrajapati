using LegacyOrderApi.Models;

namespace LegacyOrderApi.Repositories
{
    /// <summary>
    /// All EF Core / database access for orders lives behind this interface.
    /// OrderService talks to this abstraction only — it never sees DbContext,
    /// DbSet, or any EF Core types directly.
    /// </summary>
    public interface IOrderRepository
    {
        Task<Customer?> GetCustomerByIdAsync(int customerId, CancellationToken cancellationToken);

        Task<List<Product>> GetProductsByIdsAsync(
            IReadOnlyCollection<int> productIds,
            CancellationToken cancellationToken);

        /// <summary>
        /// Persists a new order (with its items) in a single SaveChanges
        /// call and returns the order with its generated Id populated.
        /// </summary>
        Task<Order> AddOrderAsync(Order order, CancellationToken cancellationToken);

        Task<Order?> GetOrderByIdAsync(int orderId, CancellationToken cancellationToken);
    }
}
