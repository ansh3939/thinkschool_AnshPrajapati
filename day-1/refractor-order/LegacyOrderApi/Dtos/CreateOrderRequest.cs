using System.ComponentModel.DataAnnotations;

namespace LegacyOrderApi.Dtos
{
    /// <summary>
    /// Inbound request for POST /api/orders.
    /// Only structural/shape validation lives here (via data annotations).
    /// Anything that requires business knowledge or DB access (does the
    /// product exist, is there enough stock, etc.) is decided in
    /// OrderService, not here.
    /// </summary>
    public class CreateOrderRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "CustomerId must be a positive number.")]
        public int CustomerId { get; set; }

        public string? DiscountCode { get; set; }

        [MinLength(1, ErrorMessage = "Order must contain at least one item.")]
        public List<CreateOrderItemRequest> Items { get; set; } = new();
    }

    public class CreateOrderItemRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "ProductId must be a positive number.")]
        public int ProductId { get; set; }

        /// <summary>
        /// Intentionally NOT constrained with a [Range] attribute here.
        /// Whether a quantity is usable depends on business rules (must be
        /// >= 1, and is capped by available stock), so OrderService decides
        /// what to do with an invalid quantity and reports it explicitly via
        /// OrderItemIssue instead of the framework silently rejecting the
        /// whole request.
        /// </summary>
        public int Quantity { get; set; }
    }
}
