using LegacyOrderApi.Data;
using LegacyOrderApi.Models;
using LegacyOrderApi.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LegacyOrderApi.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<OrderRepository> _logger;

        public OrderRepository(AppDbContext dbContext, ILogger<OrderRepository> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<Customer?> GetCustomerByIdAsync(int customerId, CancellationToken cancellationToken)
        {
            return await _dbContext.Customers
                .Include(c => c.Address)
                .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        }

        public async Task<List<Product>> GetProductsByIdsAsync(
            IReadOnlyCollection<int> productIds,
            CancellationToken cancellationToken)
        {
            if (productIds.Count == 0)
            {
                return new List<Product>();
            }

            return await _dbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync(cancellationToken);
        }

        public async Task<Order> AddOrderAsync(Order order, CancellationToken cancellationToken)
        {
            _dbContext.Orders.Add(order);

            try
            {
                // Single SaveChanges call persists the order, its items
                // (via the navigation property) and any product stock
                // updates the service already applied to tracked Product
                // entities — no duplicate saves.
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Failed to save order for customer {CustomerId}", order.CustomerId);
                throw new OrderPersistenceException("Failed to save the order to the database.", ex);
            }

            return order;
        }

        public async Task<Order?> GetOrderByIdAsync(int orderId, CancellationToken cancellationToken)
        {
            return await _dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        }
    }
}
