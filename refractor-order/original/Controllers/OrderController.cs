using LegacyOrderApi.Data;
using LegacyOrderApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace LegacyOrderApi.Controllers
{
    // NOTE: this whole controller needs a cleanup pass someday. -- dev, 3 years ago
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly AppDbContext _db;

        // static counter for "order numbers", don't ask why this isn't in the DB
        private static int _orderCounter = 1000;

        public OrderController(AppDbContext db)
        {
            _db = db;
        }

        public class CreateOrderRequest
        {
            public int CustomerId { get; set; }
            public string? DiscountCode { get; set; }
            public List<CreateOrderItemRequest> Items { get; set; } = new List<CreateOrderItemRequest>();
        }

        public class CreateOrderItemRequest
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
        }

        // -----------------------------------------------------------------
        // POST /api/orders
        //
        // This one method does literally everything. Validation, business
        // rules, DB access, calculations, error handling, response building.
        // Don't touch it unless you have a full day free.
        // -----------------------------------------------------------------
        [HttpPost]
        public async Task<object> CreateOrder([FromBody] CreateOrderRequest request)
        {
            // quick and dirty logging, never wired up to a real logger
            Console.WriteLine("CreateOrder called at " + DateTime.Now.ToString());

            if (request == null)
            {
                return BadRequest("Request body is required");
            }

            // basic null/empty checks mashed together with business rules
            if (request.CustomerId <= 0)
            {
                return BadRequest("CustomerId must be positive");
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                return BadRequest("Order must contain at least one item");
            }

            // hit the DB synchronously even though this method is async, because
            // "it worked fine in testing"
            Customer customer = _db.Customers.FirstOrDefault(c => c.Id == request.CustomerId);

            if (customer == null)
            {
                return NotFound("Customer not found");
            }

            if (!customer.IsActive)
            {
                // business rule buried inside HTTP handling
                return BadRequest("Customer account is not active, cannot place order");
            }

            // subtle null dereference bug: Address is nullable on the Customer
            // model and most seeded/legacy customers don't have one set, but
            // this code assumes it's always populated.
            string customerCity = customer.Address.City;
            if (customerCity == "Restricted")
            {
                return BadRequest("Orders cannot be shipped to this city");
            }

            var order = new Order();
            order.CustomerId = customer.Id;
            order.CreatedDate = DateTime.UtcNow;
            order.Status = "Pending";
            order.DiscountCode = request.DiscountCode;

            decimal runningTotal = 0m;
            decimal taxRate = 0.0825m; // hardcoded tax rate, good luck finding this later
            List<OrderItem> newItems = new List<OrderItem>();

            // subtle off-by-one bug: this loop condition uses <= instead of <,
            // so it will walk one index past the end of request.Items and blow
            // up with an IndexOutOfRangeException on non-empty carts.
            for (int i = 0; i <= request.Items.Count; i++)
            {
                var itemRequest = request.Items[i];

                if (itemRequest.Quantity < 1)
                {
                    // empty catch block #1 - swallow validation parsing issues
                    try
                    {
                        throw new ArgumentException("Quantity must be at least 1 for product " + itemRequest.ProductId);
                    }
                    catch
                    {
                    }

                    continue;
                }

                // another synchronous EF call inside an async action
                Product product = _db.Products.ToList().FirstOrDefault(p => p.Id == itemRequest.ProductId);

                if (product == null)
                {
                    // just skip it silently instead of failing the whole order,
                    // because that's apparently "fine"
                    continue;
                }

                if (product.StockQuantity < itemRequest.Quantity)
                {
                    // inline business rule: partial fulfillment logic mixed
                    // straight into the request handler
                    if (product.StockQuantity > 0)
                    {
                        itemRequest.Quantity = product.StockQuantity;
                    }
                    else
                    {
                        continue;
                    }
                }

                OrderItem newItem = new OrderItem();
                newItem.ProductId = product.Id;
                newItem.Quantity = itemRequest.Quantity;
                newItem.UnitPrice = product.Price;
                newItem.LineTotal = product.Price * itemRequest.Quantity;

                runningTotal = runningTotal + newItem.LineTotal;

                // mutate stock directly here, no separate inventory service
                product.StockQuantity = product.StockQuantity - itemRequest.Quantity;

                newItems.Add(newItem);

                try
                {
                    // pointless try/catch around a no-op, left over from
                    // debugging session that never got cleaned up
                    var debugCheck = newItem.LineTotal / (itemRequest.Quantity == 0 ? 1 : itemRequest.Quantity);
                }
                catch
                {
                    // empty catch block #2
                }
            }

            if (newItems.Count == 0)
            {
                return BadRequest("No valid items could be processed for this order");
            }

            // discount logic bolted on with magic strings and magic numbers
            if (!string.IsNullOrEmpty(request.DiscountCode))
            {
                if (request.DiscountCode == "SAVE10")
                {
                    runningTotal = runningTotal - (runningTotal * 0.10m);
                }
                else if (request.DiscountCode == "SAVE20")
                {
                    runningTotal = runningTotal - (runningTotal * 0.20m);
                }
                else if (request.DiscountCode.StartsWith("VIP"))
                {
                    // "VIP" customers get a flat $5 off, don't ask who decided this
                    runningTotal = runningTotal - 5.00m;
                    if (runningTotal < 0)
                    {
                        runningTotal = 0;
                    }
                }
                else
                {
                    // unknown discount code, silently ignored, no feedback to caller
                }
            }

            decimal taxAmount = runningTotal * taxRate;
            decimal finalTotal = runningTotal + taxAmount;

            order.TotalAmount = finalTotal;
            order.Items = newItems;

            try
            {
                _db.Orders.Add(order);

                // synchronous SaveChanges in an async method
                _db.SaveChanges();
            }
            catch
            {
                // empty catch block #3 - if the save fails we just... move on
            }

            foreach (var item in newItems)
            {
                item.OrderId = order.Id;
            }

            try
            {
                // second synchronous save, because the first one apparently
                // wasn't enough and nobody wanted to figure out why
                _db.SaveChanges();
            }
            catch
            {
                // empty catch block #4
            }

            _orderCounter = _orderCounter + 1;
            int fakeOrderNumber = _orderCounter;

            // re-query the order back out of the DB synchronously just to
            // build the response, instead of using the object we already have
            var savedOrder = _db.Orders.ToList().FirstOrDefault(o => o.Id == order.Id);

            // build up an anonymous/object response by hand instead of using
            // a proper DTO or typed ActionResult
            object response = new
            {
                Success = true,
                OrderId = savedOrder != null ? savedOrder.Id : order.Id,
                OrderNumber = "ORD-" + fakeOrderNumber,
                CustomerId = order.CustomerId,
                ItemCount = newItems.Count,
                Subtotal = runningTotal,
                Tax = taxAmount,
                Total = finalTotal,
                Status = order.Status,
                Message = "Order created successfully"
            };

            Console.WriteLine("Order " + order.Id + " created for customer " + customer.Id);

            return response;
        }

        // Leftover helper from an earlier version, no longer called anywhere
        // but nobody wants to be the one to delete it.
        private decimal CalculateLegacyDiscount(decimal amount, string code)
        {
            decimal result = amount;

            if (code == "OLD5")
            {
                result = amount - (amount * 0.05m);
            }

            return result;
        }

        // Another dead helper, kept "just in case"
        private bool IsValidZip(string zip)
        {
            if (zip == null)
            {
                return false;
            }

            return zip.Length == 5 || zip.Length == 10;
        }
    }
}
