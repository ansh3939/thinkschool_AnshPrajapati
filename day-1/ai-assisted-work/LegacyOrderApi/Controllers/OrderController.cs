using LegacyOrderApi.Dtos;
using LegacyOrderApi.Services;
using LegacyOrderApi.Services.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace LegacyOrderApi.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrderController> _logger;

        public OrderController(
            IOrderService orderService,
            ILogger<OrderController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new order.
        /// Business logic and persistence are handled by the service
        /// and repository layers.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(
            typeof(OrderResponse),
            StatusCodes.Status201Created)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status400BadRequest)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status404NotFound)]
        [ProducesResponseType(
            typeof(ProblemDetails),
            StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OrderResponse>> CreateOrder(
            [FromBody] CreateOrderRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var response = await _orderService.CreateOrderAsync(
                    request,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetOrder),
                    new { id = response.OrderId },
                    response);
            }
            catch (CustomerNotFoundException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Order creation failed: customer not found.");

                return Problem(
                    ex.Message,
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Customer not found");
            }
            catch (CustomerNotActiveException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Order creation failed: customer is not active.");

                return Problem(
                    ex.Message,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Customer not active");
            }
            catch (RestrictedShippingCityException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Order creation failed: restricted shipping city.");

                return Problem(
                    ex.Message,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Shipping not allowed");
            }
            catch (EmptyOrderException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Order creation failed: no valid items.");

                return Problem(
                    ex.Message,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "No valid items");
            }
            catch (OrderPersistenceException ex)
            {
                _logger.LogError(
                    ex,
                    "Order creation failed: persistence error.");

                return Problem(
                    ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Failed to save order");
            }
        }

        /// <summary>
        /// Retrieves a previously created order.
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(
            typeof(OrderResponse),
            StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OrderResponse>> GetOrder(
            int id,
            CancellationToken cancellationToken)
        {
            var order = await _orderService.GetOrderByIdAsync(
                id,
                cancellationToken);

            if (order == null)
            {
                return NotFound();
            }

            return Ok(order);
        }
    }
}