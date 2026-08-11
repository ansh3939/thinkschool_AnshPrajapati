namespace LegacyOrderApi.Services.Discounts
{
    /// <summary>
    /// Single responsibility: handle the SAVE20 code (20% off subtotal).
    /// </summary>
    public class SaveTwentyDiscountStrategy : IDiscountStrategy
    {
        public bool CanApply(string discountCode) => discountCode == "SAVE20";

        public decimal Apply(decimal subtotal) => subtotal - (subtotal * 0.20m);
    }
}
