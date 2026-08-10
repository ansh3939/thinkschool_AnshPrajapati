using System;

namespace LegacyOrderApi.Services.Exceptions
{
    /// <summary>
    /// Base type for exceptions that represent an expected business-rule
    /// failure (as opposed to an unexpected/infrastructure failure).
    /// The controller catches these specific types and maps them to the
    /// appropriate HTTP status code.
    /// </summary>
    public abstract class OrderDomainException : Exception
    {
        protected OrderDomainException(string message) : base(message)
        {
        }

        protected OrderDomainException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class CustomerNotFoundException : OrderDomainException
    {
        public int CustomerId { get; }

        public CustomerNotFoundException(int customerId)
            : base($"Customer {customerId} was not found.")
        {
            CustomerId = customerId;
        }
    }

    public sealed class CustomerNotActiveException : OrderDomainException
    {
        public int CustomerId { get; }

        public CustomerNotActiveException(int customerId)
            : base($"Customer {customerId} is not active and cannot place orders.")
        {
            CustomerId = customerId;
        }
    }

    public sealed class RestrictedShippingCityException : OrderDomainException
    {
        public RestrictedShippingCityException(string city)
            : base($"Orders cannot be shipped to '{city}'.")
        {
        }
    }

    /// <summary>
    /// Thrown when none of the requested items could be fulfilled
    /// (e.g. every product was invalid, unknown, or out of stock).
    /// </summary>
    public sealed class EmptyOrderException : OrderDomainException
    {
        public EmptyOrderException()
            : base("No valid items could be processed for this order.")
        {
        }
    }

    /// <summary>
    /// Wraps a lower-level persistence failure (e.g. DbUpdateException) so
    /// the caller doesn't need to know about EF Core specifically.
    /// </summary>
    public sealed class OrderPersistenceException : OrderDomainException
    {
        public OrderPersistenceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
