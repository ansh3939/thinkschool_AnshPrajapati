using LegacyOrderApi.Models;

namespace LegacyOrderApi.Services.Rules
{
    /// <summary>
    /// A single customer-eligibility rule that must pass before an order
    /// can be created. Each implementation inspects the customer and
    /// throws its own specific exception if the customer fails it.
    /// </summary>
    public interface IOrderEligibilityRule
    {
        void Validate(Customer customer);
    }
}
