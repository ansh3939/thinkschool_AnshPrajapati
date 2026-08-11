namespace LegacyOrderApi.Services.Discounts
{
    /// <summary>
    /// A single discount-code rule. CanApply tells the service whether
    /// this strategy handles the given code; Apply performs the discount
    /// math for that code.
    /// </summary>
    public interface IDiscountStrategy
    {
        bool CanApply(string discountCode);

        decimal Apply(decimal subtotal);
    }
}
