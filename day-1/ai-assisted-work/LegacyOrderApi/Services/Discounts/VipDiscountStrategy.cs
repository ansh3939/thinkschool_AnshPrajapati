using System;

namespace LegacyOrderApi.Services.Discounts
{
    /// <summary>
    /// Single responsibility: handle any VIP*-prefixed code (flat $5 off,
    /// never going below zero).
    /// </summary>
    public class VipDiscountStrategy : IDiscountStrategy
    {
        public bool CanApply(string discountCode) =>
            discountCode.StartsWith("VIP", StringComparison.OrdinalIgnoreCase);

        public decimal Apply(decimal subtotal)
        {
            var discounted = subtotal - 5.00m;
            return discounted < 0 ? 0 : discounted;
        }
    }
}
