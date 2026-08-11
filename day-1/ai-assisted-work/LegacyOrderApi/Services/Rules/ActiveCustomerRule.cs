using LegacyOrderApi.Models;
using LegacyOrderApi.Services.Exceptions;

namespace LegacyOrderApi.Services.Rules
{
    /// <summary>
    /// Single responsibility: reject orders for customers whose account
    /// is not active.
    /// </summary>
    public class ActiveCustomerRule : IOrderEligibilityRule
    {
        public void Validate(Customer customer)
        {
            if (!customer.IsActive)
            {
                throw new CustomerNotActiveException(customer.Id);
            }
        }
    }
}
