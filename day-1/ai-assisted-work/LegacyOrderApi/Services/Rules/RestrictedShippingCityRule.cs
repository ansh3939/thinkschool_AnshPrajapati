using LegacyOrderApi.Models;
using LegacyOrderApi.Services.Exceptions;

namespace LegacyOrderApi.Services.Rules
{
    /// <summary>
    /// Single responsibility: reject orders for customers whose address
    /// is in a restricted shipping city. Customers with no address on
    /// file (Address is optional) are allowed through.
    /// </summary>
    public class RestrictedShippingCityRule : IOrderEligibilityRule
    {
        private const string RestrictedCity = "Restricted";

        public void Validate(Customer customer)
        {
            var city = customer.Address?.City;
            if (!string.IsNullOrEmpty(city) &&
                string.Equals(city, RestrictedCity, StringComparison.OrdinalIgnoreCase))
            {
                throw new RestrictedShippingCityException(city);
            }
        }
    }
}
