namespace LegacyOrderApi.Services.Discounts
{
    /// <summary>
    /// Single responsibility: handle the SAVE10 code (10% off subtotal).
    /// </summary>
    public class SaveTenDiscountStrategy : IDiscountStrategy
    {
        public bool CanApply(string discountCode) => discountCode == "SAVE10";

        public decimal Apply(decimal subtotal) => subtotal - (subtotal * 0.10m);
    }
}
