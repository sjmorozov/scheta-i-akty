using System;
using System.Collections.Generic;
using System.Linq;

namespace InvoiceAndAct
{
    internal sealed class AmountOption
    {
        public decimal Quantity { get; private set; }
        public decimal Price { get; private set; }

        public AmountOption(decimal quantity, decimal price)
        {
            Quantity = quantity;
            Price = price;
        }
    }

    internal static class AmountOptions
    {
        public static IList<AmountOption> Find(decimal amount, bool byQuantity, decimal? fixedValue)
        {
            if (amount <= 0 || amount >= Money.Limit || Money.Round(amount) != amount)
                throw new ArgumentException("Укажи положительную сумму с точностью до копейки.");
            if (fixedValue.HasValue)
            {
                if (fixedValue.Value <= 0)
                    throw new ArgumentException("Фиксированное значение должно быть больше нуля.");
                return WithFixedValue(amount, byQuantity, fixedValue.Value);
            }

            long cents = (long)(amount * 100);
            var pairs = new Dictionary<decimal, AmountOption>();
            for (long divisor = 1; divisor <= cents / divisor; divisor++)
            {
                if (cents % divisor != 0)
                    continue;
                AddPairs(pairs, cents, divisor);
                if (divisor != cents / divisor)
                    AddPairs(pairs, cents, cents / divisor);
            }

            if (byQuantity)
                return pairs.Values.OrderBy(option => option.Quantity % 1 != 0)
                    .ThenBy(option => option.Quantity).ToList();
            return pairs.Values.OrderBy(option => option.Price % 1 != 0)
                .ThenBy(option => option.Quantity % 1 != 0)
                .ThenByDescending(option => option.Price).ToList();
        }

        private static void AddPairs(Dictionary<decimal, AmountOption> pairs, long cents, long divisor)
        {
            decimal basePrice = (cents / divisor) / 100m;
            // Besides whole units, suggest common fractional quantities such as 0.25 and 0.5.
            foreach (int scale in new[] { 1, 2, 4, 5, 10 })
            {
                decimal quantity = (decimal)divisor / scale;
                decimal price = basePrice * scale;
                if (quantity < Money.Limit && price < Money.Limit && !pairs.ContainsKey(quantity))
                    pairs.Add(quantity, new AmountOption(quantity, price));
            }
        }

        private static IList<AmountOption> WithFixedValue(decimal amount, bool byQuantity, decimal value)
        {
            decimal quotient = amount / value;
            decimal scale = byQuantity ? 100m : 1000000m;
            var possible = new[] {
                decimal.Floor(quotient * scale) / scale,
                decimal.Ceiling(quotient * scale) / scale
            };
            return possible.Distinct()
                .Select(candidate => byQuantity ? new AmountOption(value, candidate) : new AmountOption(candidate, value))
                .Where(option => option.Quantity > 0 && option.Quantity < Money.Limit && option.Price < Money.Limit &&
                    Money.Round(option.Quantity * option.Price) == amount)
                .ToList();
        }
    }
}
