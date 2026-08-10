namespace LegacyOrderApi.Dtos
{
    /// <summary>
    /// Strongly typed response returned from the order endpoints, replacing
    /// the legacy controller's anonymous "object" response.
    /// </summary>
    public class OrderResponse
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public List<OrderItemResponse> Items { get; set; } = new();

        /// <summary>
        /// Requested items that were not fully honored (invalid quantity,
        /// unknown product, out of stock, or reduced due to limited stock).
        /// The legacy controller silently dropped these; the refactor makes
        /// them explicit instead.
        /// </summary>
        public List<OrderItemIssue> Issues { get; set; } = new();
    }

    public class OrderItemResponse
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public enum OrderItemIssueType
    {
        InvalidQuantity,
        ProductNotFound,
        OutOfStock,
        QuantityReducedForStock
    }

    public class OrderItemIssue
    {
        public int ProductId { get; set; }
        public int RequestedQuantity { get; set; }
        public int? AdjustedQuantity { get; set; }
        public OrderItemIssueType IssueType { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
