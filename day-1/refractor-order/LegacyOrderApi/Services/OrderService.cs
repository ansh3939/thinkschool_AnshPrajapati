using LegacyOrderApi.Dtos;
using LegacyOrderApi.Models;
using LegacyOrderApi.Repositories;
using LegacyOrderApi.Services.Exceptions;

namespace LegacyOrderApi.Services
{
    public class OrderService : IOrderService
    {
        private const decimal TaxRate = 0.0825m;
        private const string RestrictedCity = "Restricted";

        private readonly IOrderRepository _orderRepository;
        private readonly ILogger<OrderService> _logger;

        public OrderService(IOrderRepository orderRepository, ILogger<OrderService> logger)
        {
            _orderRepository = orderRepository;
            _logger = logger;
        }

        public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var customer = await _orderRepository.GetCustomerByIdAsync(request.CustomerId, cancellationToken);
            if (customer == null)
            {
                throw new CustomerNotFoundException(request.CustomerId);
            }

            if (!customer.IsActive)
            {
                throw new CustomerNotActiveException(customer.Id);
            }

            // Fix for the null-dereference bug in the legacy controller:
            // Address is optional on Customer, so we only inspect it when
            // it's actually set, instead of assuming it's always populated.
            var customerCity = customer.Address?.City;
            if (!string.IsNullOrEmpty(customerCity) &&
                string.Equals(customerCity, RestrictedCity, StringComparison.OrdinalIgnoreCase))
            {
                throw new RestrictedShippingCityException(customerCity);
            }

            var requestedProductIds = request.Items
                .Select(i => i.ProductId)
                .Distinct()
                .ToList();

            var products = await _orderRepository.GetProductsByIdsAsync(requestedProductIds, cancellationToken);
            var productsById = products.ToDictionary(p => p.Id);

            var orderItems = new List<OrderItem>();
            var itemResponses = new List<OrderItemResponse>();
            var issues = new List<OrderItemIssue>();
            decimal subtotal = 0m;

            // Fix for the off-by-one bug in the legacy controller: iterate
            // with a plain foreach (equivalent to `i < Count`, never `<=`),
            // so every requested item is visited exactly once and nothing
            // reads past the end of the list.
            foreach (var itemRequest in request.Items)
            {
                if (itemRequest.Quantity < 1)
                {
                    issues.Add(new OrderItemIssue
                    {
                        ProductId = itemRequest.ProductId,
                        RequestedQuantity = itemRequest.Quantity,
                        IssueType = OrderItemIssueType.InvalidQuantity,
                        Message = "Quantity must be at least 1."
                    });
                    continue;
                }

                if (!productsById.TryGetValue(itemRequest.ProductId, out var product))
                {
                    issues.Add(new OrderItemIssue
                    {
                        ProductId = itemRequest.ProductId,
                        RequestedQuantity = itemRequest.Quantity,
                        IssueType = OrderItemIssueType.ProductNotFound,
                        Message = $"Product {itemRequest.ProductId} does not exist."
                    });
                    continue;
                }

                var quantityToFulfill = itemRequest.Quantity;

                if (product.StockQuantity < quantityToFulfill)
                {
                    if (product.StockQuantity <= 0)
                    {
                        issues.Add(new OrderItemIssue
                        {
                            ProductId = product.Id,
                            RequestedQuantity = itemRequest.Quantity,
                            IssueType = OrderItemIssueType.OutOfStock,
                            Message = $"Product {product.Id} is out of stock."
                        });
                        continue;
                    }

                    // Preserve the legacy partial-fulfillment behavior, but
                    // report it explicitly instead of silently changing the
                    // quantity with no trace in the response.
                    issues.Add(new OrderItemIssue
                    {
                        ProductId = product.Id,
                        RequestedQuantity = itemRequest.Quantity,
                        AdjustedQuantity = product.StockQuantity,
                        IssueType = OrderItemIssueType.QuantityReducedForStock,
                        Message = $"Only {product.StockQuantity} unit(s) of product {product.Id} were in " +
                                  $"stock; quantity was reduced from {itemRequest.Quantity}."
                    });

                    quantityToFulfill = product.StockQuantity;
                }

                var lineTotal = product.Price * quantityToFulfill;
                subtotal += lineTotal;

                // Reduce tracked stock; this is persisted in the same
                // SaveChanges call that persists the order (see
                // OrderRepository.AddOrderAsync).
                product.StockQuantity -= quantityToFulfill;

                orderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = quantityToFulfill,
                    UnitPrice = product.Price,
                    LineTotal = lineTotal
                });

                itemResponses.Add(new OrderItemResponse
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = quantityToFulfill,
                    UnitPrice = product.Price,
                    LineTotal = lineTotal
                });
            }

            if (orderItems.Count == 0)
            {
                throw new EmptyOrderException();
            }

            subtotal = ApplyDiscount(subtotal, request.DiscountCode);
            var tax = Math.Round(subtotal * TaxRate, 2, MidpointRounding.AwayFromZero);
            var total = subtotal + tax;

            var order = new Order
            {
                CustomerId = customer.Id,
                CreatedDate = DateTime.UtcNow,
                Status = "Pending",
                DiscountCode = request.DiscountCode,
                TotalAmount = total,
                Items = orderItems
            };

            var savedOrder = await _orderRepository.AddOrderAsync(order, cancellationToken);

            _logger.LogInformation(
                "Created order {OrderId} for customer {CustomerId} with {ItemCount} item(s) and {IssueCount} issue(s)",
                savedOrder.Id, customer.Id, orderItems.Count, issues.Count);

            return new OrderResponse
            {
                OrderId = savedOrder.Id,
                OrderNumber = FormatOrderNumber(savedOrder.Id),
                CustomerId = savedOrder.CustomerId,
                Status = savedOrder.Status,
                CreatedDate = savedOrder.CreatedDate,
                Subtotal = subtotal,
                Tax = tax,
                Total = total,
                Items = itemResponses,
                Issues = issues
            };
        }

        public async Task<OrderResponse?> GetOrderByIdAsync(int orderId, CancellationToken cancellationToken)
        {
            var order = await _orderRepository.GetOrderByIdAsync(orderId, cancellationToken);
            if (order == null)
            {
                return null;
            }

            var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _orderRepository.GetProductsByIdsAsync(productIds, cancellationToken);
            var productNamesById = products.ToDictionary(p => p.Id, p => p.Name);

            var subtotal = order.Items.Sum(i => i.LineTotal);
            var tax = order.TotalAmount - subtotal;

            return new OrderResponse
            {
                OrderId = order.Id,
                OrderNumber = FormatOrderNumber(order.Id),
                CustomerId = order.CustomerId,
                Status = order.Status,
                CreatedDate = order.CreatedDate,
                Subtotal = subtotal,
                Tax = tax,
                Total = order.TotalAmount,
                Items = order.Items.Select(i => new OrderItemResponse
                {
                    ProductId = i.ProductId,
                    ProductName = productNamesById.TryGetValue(i.ProductId, out var name) ? name : "Unknown",
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    LineTotal = i.LineTotal
                }).ToList()
            };
        }

        private static string FormatOrderNumber(int orderId) => $"ORD-{orderId:D6}";

        /// <summary>
        /// Same discount rules as the legacy controller (SAVE10, SAVE20,
        /// any VIP* code for a flat $5 off, unknown codes ignored), just
        /// isolated from HTTP/DB concerns so it can be unit tested directly.
        /// </summary>
        private decimal ApplyDiscount(decimal subtotal, string? discountCode)
        {
            if (string.IsNullOrEmpty(discountCode))
            {
                return subtotal;
            }

            if (discountCode == "SAVE10")
            {
                return subtotal - (subtotal * 0.10m);
            }

            if (discountCode == "SAVE20")
            {
                return subtotal - (subtotal * 0.20m);
            }

            if (discountCode.StartsWith("VIP", StringComparison.OrdinalIgnoreCase))
            {
                var discounted = subtotal - 5.00m;
                return discounted < 0 ? 0 : discounted;
            }

            _logger.LogInformation("Unrecognized discount code {DiscountCode}; no discount applied", discountCode);
            return subtotal;
        }
    }
}
