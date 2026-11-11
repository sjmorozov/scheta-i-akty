using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace InvoiceAndAct
{
    [DataContract]
    internal sealed class Party
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public string Inn { get; set; }
        [DataMember] public string Kpp { get; set; }
        [DataMember] public string Address { get; set; }
        [DataMember] public string Signatory { get; set; }

        public string TaxDetails()
        {
            return string.Join(", ", new[] {
                string.IsNullOrEmpty(Inn) ? "" : "ИНН " + Inn,
                string.IsNullOrEmpty(Kpp) ? "" : "КПП " + Kpp
            }.Where(value => value.Length > 0));
        }

        public string Description()
        {
            return string.Join(", ", new[] { Name, TaxDetails(), Address }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        public void Validate(string role, bool requireInn)
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException(role + ". Укажи наименование или ФИО.");
            if ((requireInn || !string.IsNullOrEmpty(Inn)) && !Regex.IsMatch(Inn ?? "", @"\A(?:[0-9]{10}|[0-9]{12})\z"))
                throw new ArgumentException(role + ". В ИНН должно быть 10 или 12 цифр.");
            if (!string.IsNullOrEmpty(Kpp) && !Regex.IsMatch(Kpp, @"\A[0-9]{4}[0-9A-Z]{2}[0-9]{3}\z"))
                throw new ArgumentException(role + ". Проверь КПП.");
        }
    }

    [DataContract]
    internal sealed class Supplier
    {
        [DataMember] public Party Party { get; set; }
        [DataMember] public string Bank { get; set; }
        [DataMember] public string Bic { get; set; }
        [DataMember] public string Account { get; set; }
        [DataMember] public string Correspondent { get; set; }
        [DataMember] public string Accountant { get; set; }

        public Supplier()
        {
            Party = new Party();
        }

        public void Validate()
        {
            Party.Validate("Поставщик", true);
            if (string.IsNullOrWhiteSpace(Party.Address) || string.IsNullOrWhiteSpace(Bank))
                throw new ArgumentException("Укажи адрес поставщика и название банка.");
            if (!Regex.IsMatch(Bic ?? "", @"\A[0-9]{9}\z"))
                throw new ArgumentException("В БИК должно быть 9 цифр.");
            if (!Regex.IsMatch(Account ?? "", @"\A[0-9]{20}\z") ||
                !Regex.IsMatch(Correspondent ?? "", @"\A[0-9]{20}\z"))
                throw new ArgumentException("В расчётном и корреспондентском счетах должно быть по 20 цифр.");
        }
    }

    internal sealed class LineItem
    {
        public string Name { get; private set; }
        public string Unit { get; private set; }
        public decimal Quantity { get; private set; }
        public decimal Price { get; private set; }
        public decimal Amount { get; private set; }

        public LineItem(string name, string unit, string quantity, string price, int row)
        {
            string prefix = "Строка " + row + ". ";
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(prefix + "Укажи наименование.");
            if (string.IsNullOrWhiteSpace(unit))
                throw new ArgumentException(prefix + "Укажи единицу измерения.");

            Name = name.Trim();
            Unit = unit.Trim();
            Quantity = Parse(quantity, 6, prefix + "Проверь количество. Допустимо до 6 знаков после запятой.");
            if (Quantity == 0)
                throw new ArgumentException(prefix + "Количество должно быть больше нуля.");
            Price = Parse(price, 2, prefix + "Проверь цену. Допустимо до 2 знаков после запятой.");
            Amount = Money.Round(Quantity * Price);
            if (Amount >= Money.Limit)
                throw new ArgumentException(prefix + "Сумма строки должна быть меньше триллиона рублей.");
        }

        private static decimal Parse(string text, int precision, string message)
        {
            string value = Regex.Replace(text ?? "", @"\s+", "").Replace(',', '.');
            decimal result;
            if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result) ||
                result >= Money.Limit || result != decimal.Round(result, precision))
                throw new ArgumentException(message);
            return result;
        }
    }

    internal sealed class Invoice
    {
        public const int MaxNumber = 999999999;
        public int Number { get; private set; }
        public DateTime InvoiceDate { get; private set; }
        public DateTime ActDate { get; private set; }
        public Supplier Supplier { get; private set; }
        public Party Buyer { get; private set; }
        public IList<LineItem> Items { get; private set; }
        public decimal? VatRate { get; private set; }
        public decimal Total { get; private set; }
        public decimal VatAmount { get; private set; }

        public Invoice(int number, DateTime invoiceDate, DateTime actDate, Supplier supplier, Party buyer,
            IEnumerable<LineItem> items, decimal? vatRate)
        {
            if (number < 1 || number > MaxNumber)
                throw new ArgumentException("Укажи номер счёта от 1 до " + MaxNumber + ".");
            supplier.Validate();
            buyer.Validate("Покупатель", false);
            Number = number;
            InvoiceDate = invoiceDate.Date;
            ActDate = actDate.Date;
            Supplier = supplier;
            Buyer = buyer;
            Items = items.ToList().AsReadOnly();
            if (Items.Count == 0)
                throw new ArgumentException("Добавь хотя бы одну позицию.");
            Total = Items.Sum(item => item.Amount);
            if (Total >= Money.Limit)
                throw new ArgumentException("Общая сумма должна быть меньше триллиона рублей.");
            VatRate = vatRate;
            if (vatRate.HasValue)
                VatAmount = Money.Round(Total * vatRate.Value / (100 + vatRate.Value));
        }
    }

    internal static class Money
    {
        public static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");
        public const decimal Limit = 1000000000000m;

        private static readonly string[] Units =
            { "", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять" };
        private static readonly string[] Teens =
            { "десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать",
              "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать" };
        private static readonly string[] Tens =
            { "", "", "двадцать", "тридцать", "сорок", "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто" };
        private static readonly string[] Hundreds =
            { "", "сто", "двести", "триста", "четыреста", "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот" };

        public static decimal Round(decimal value)
        {
            return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        public static string Format(decimal value)
        {
            return value.ToString("N2", Russian);
        }

        public static string Quantity(decimal value)
        {
            return value.ToString("0.######", Russian);
        }

        public static string InWords(decimal amount)
        {
            long rubles = (long)amount;
            int kopecks = (int)((amount - rubles) * 100);
            var words = new List<string>();
            AddGroup(words, (int)(rubles / 1000000000), false, "миллиард", "миллиарда", "миллиардов");
            AddGroup(words, (int)(rubles / 1000000 % 1000), false, "миллион", "миллиона", "миллионов");
            AddGroup(words, (int)(rubles / 1000 % 1000), true, "тысяча", "тысячи", "тысяч");
            AddGroup(words, (int)(rubles % 1000), false, null, null, null);
            if (words.Count == 0)
                words.Add("ноль");
            words.Add(Decline(rubles, "рубль", "рубля", "рублей"));
            words.Add(kopecks.ToString("00", CultureInfo.InvariantCulture));
            words.Add(Decline(kopecks, "копейка", "копейки", "копеек"));
            string result = string.Join(" ", words);
            return char.ToUpper(result[0], Russian) + result.Substring(1);
        }

        private static void AddGroup(List<string> words, int value, bool feminine, string one, string few, string many)
        {
            if (value == 0)
                return;
            if (value >= 100)
                words.Add(Hundreds[value / 100]);
            int lastTwo = value % 100;
            if (lastTwo >= 10 && lastTwo < 20)
                words.Add(Teens[lastTwo - 10]);
            else
            {
                if (lastTwo >= 20)
                    words.Add(Tens[lastTwo / 10]);
                int unit = lastTwo % 10;
                if (unit > 0)
                    words.Add(feminine && unit == 1 ? "одна" : feminine && unit == 2 ? "две" : Units[unit]);
            }
            if (one != null)
                words.Add(Decline(value, one, few, many));
        }

        private static string Decline(long value, string one, string few, string many)
        {
            long lastTwo = value % 100;
            if (lastTwo >= 11 && lastTwo <= 14)
                return many;
            long last = value % 10;
            return last == 1 ? one : last >= 2 && last <= 4 ? few : many;
        }
    }
}
