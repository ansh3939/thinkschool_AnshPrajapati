using System.Net;
using System.Net.Http.Json;
using LegacyOrderApi.Dtos;
using Xunit;

namespace LegacyOrderApi.Tests
{
    public class OrderControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public OrderControllerIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task PostOrders_ForActiveCustomerWithNoAddress_ReturnsCreatedWithCorrectTotal()
        {
            // This exact request would fail against the original legacy
            // controller for two independent reasons:
            //   1. Customer 1 (seeded in Program.cs) has Address == null,
            //      and the legacy code dereferences customer.Address.City
            //      unconditionally -> NullReferenceException.
            //   2. Even for a customer with an address, the legacy loop
            //      `for (int i = 0; i <= request.Items.Count; i++)` reads one
            //      index past the end of any non-empty item list ->
            //      IndexOutOfRangeException.
            // After the refactor, both are fixed and this returns 201 with a
            // correctly calculated total.
            var client = _factory.CreateClient();

            var request = new CreateOrderRequest
            {
                CustomerId = 1, // Alice Johnson: active, Address == null
                Items = new List<CreateOrderItemRequest>
                {
                    new() { ProductId = 1, Quantity = 2 } // Widget @ 9.99
                }
            };

            // Act
            var httpResponse = await client.PostAsJsonAsync("/api/orders", request);

            // Assert
            Assert.Equal(HttpStatusCode.Created, httpResponse.StatusCode);

            var order = await httpResponse.Content.ReadFromJsonAsync<OrderResponse>();
            Assert.NotNull(order);
            Assert.Equal(1, order!.CustomerId);
            Assert.Single(order.Items);
            Assert.Empty(order.Issues);

            var expectedSubtotal = 2 * 9.99m;
            Assert.Equal(expectedSubtotal, order.Subtotal);

            var expectedTax = Math.Round(expectedSubtotal * 0.0825m, 2, MidpointRounding.AwayFromZero);
            Assert.Equal(expectedTax, order.Tax);
            Assert.Equal(expectedSubtotal + expectedTax, order.Total);
        }
    }
}
