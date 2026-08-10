using LegacyOrderApi.Dtos;
using LegacyOrderApi.Models;
using LegacyOrderApi.Repositories;
using LegacyOrderApi.Services;
using LegacyOrderApi.Services.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LegacyOrderApi.Tests
{
    public class OrderServiceTests
    {
        private static OrderService CreateSut(Mock<IOrderRepository> repositoryMock)
        {
            return new OrderService(repositoryMock.Object, NullLogger<OrderService>.Instance);
        }

        [Fact]
        public async Task CreateOrderAsync_WhenCustomerDoesNotExist_ThrowsCustomerNotFoundException()
        {
            // Arrange
            var repositoryMock = new Mock<IOrderRepository>();
            repositoryMock
                .Setup(r => r.GetCustomerByIdAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Customer?)null);

            var sut = CreateSut(repositoryMock);

            var request = new CreateOrderRequest
            {
                CustomerId = 42,
                Items = new List<CreateOrderItemRequest>
                {
                    new() { ProductId = 1, Quantity = 1 }
                }
            };

            // Act & Assert
            await Assert.ThrowsAsync<CustomerNotFoundException>(
                () => sut.CreateOrderAsync(request, CancellationToken.None));
        }

        [Fact]
        public async Task CreateOrderAsync_WhenCustomerIsNotActive_ThrowsCustomerNotActiveException()
        {
            // Arrange
            var inactiveCustomer = new Customer { Id = 1, Name = "Inactive Customer", IsActive = false };

            var repositoryMock = new Mock<IOrderRepository>();
            repositoryMock
                .Setup(r => r.GetCustomerByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(inactiveCustomer);

            var sut = CreateSut(repositoryMock);

            var request = new CreateOrderRequest
            {
                CustomerId = 1,
                Items = new List<CreateOrderItemRequest>
                {
                    new() { ProductId = 1, Quantity = 1 }
                }
            };

            // Act & Assert
            await Assert.ThrowsAsync<CustomerNotActiveException>(
                () => sut.CreateOrderAsync(request, CancellationToken.None));
        }

        [Fact]
        public async Task CreateOrderAsync_WithMultipleValidItems_ProcessesEveryItemAndCalculatesCorrectTotal()
        {
            // Arrange.
            // This test exercises the exact scenario that broke the legacy
            // controller: a non-empty item list processed via a loop. In the
            // original `for (int i = 0; i <= request.Items.Count; i++)` this
            // would throw an IndexOutOfRangeException. Here we assert all
            // three items are processed and the total is calculated
            // correctly, proving the off-by-one bug is fixed.
            var activeCustomer = new Customer { Id = 1, Name = "Active Customer", IsActive = true, Address = null };

            var products = new List<Product>
            {
                new() { Id = 1, Name = "Widget", Price = 10.00m, StockQuantity = 100 },
                new() { Id = 2, Name = "Gadget", Price = 20.00m, StockQuantity = 100 },
                new() { Id = 3, Name = "Gizmo", Price = 5.00m, StockQuantity = 100 }
            };

            Order? capturedOrder = null;

            var repositoryMock = new Mock<IOrderRepository>();
            repositoryMock
                .Setup(r => r.GetCustomerByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(activeCustomer);
            repositoryMock
                .Setup(r => r.GetProductsByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(products);
            repositoryMock
                .Setup(r => r.AddOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
                .Callback<Order, CancellationToken>((order, _) => capturedOrder = order)
                .ReturnsAsync((Order order, CancellationToken _) =>
                {
                    order.Id = 555;
                    return order;
                });

            var sut = CreateSut(repositoryMock);

            var request = new CreateOrderRequest
            {
                CustomerId = 1,
                Items = new List<CreateOrderItemRequest>
                {
                    new() { ProductId = 1, Quantity = 2 }, // 2 * 10.00 = 20.00
                    new() { ProductId = 2, Quantity = 1 }, // 1 * 20.00 = 20.00
                    new() { ProductId = 3, Quantity = 3 }  // 3 * 5.00  = 15.00
                }
            };

            // Act
            var response = await sut.CreateOrderAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal(3, response.Items.Count);
            Assert.Empty(response.Issues);
            Assert.Equal(55.00m, response.Subtotal);

            var expectedTax = Math.Round(55.00m * 0.0825m, 2, MidpointRounding.AwayFromZero);
            Assert.Equal(expectedTax, response.Tax);
            Assert.Equal(55.00m + expectedTax, response.Total);

            Assert.NotNull(capturedOrder);
            Assert.Equal(3, capturedOrder!.Items.Count);
        }
    }
}
