using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace InvoiceAndAct
{
    [DataContract]
    internal sealed class WorkTemplate
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public string Unit { get; set; }
        [DataMember] public decimal? Price { get; set; }
        [DataMember] public DateTime UpdatedUtc { get; set; }
    }

    internal sealed class WorkCatalog
    {
        private static readonly WorkTemplate[] Presets = {
            new WorkTemplate { Name = "Услуги спецтехники", Unit = "ч" },
            new WorkTemplate { Name = "Услуги экскаватора-погрузчика", Unit = "ч" }
        };
        private readonly string directory;

        public WorkCatalog(string directory)
        {
            this.directory = directory;
        }

        public IList<WorkTemplate> Read()
        {
            IEnumerable<WorkTemplate> saved = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "*.json").Select(Json.Read<WorkTemplate>)
                : Enumerable.Empty<WorkTemplate>();
            return saved.Where(work => work != null && !string.IsNullOrWhiteSpace(work.Name) &&
                    !string.IsNullOrWhiteSpace(work.Unit))
                .OrderByDescending(work => work.UpdatedUtc)
                .Concat(Presets)
                .GroupBy(work => Key(work.Name, work.Unit))
                .Select(group => group.First())
                .ToList();
        }

        public bool Remember(string name, string unit, string priceText)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unit))
                return false;
            string key = Key(name, unit);
            string path = Path.Combine(directory, FileName(key));
            WorkTemplate previous = File.Exists(path) ? Json.Read<WorkTemplate>(path)
                : Presets.FirstOrDefault(work => Key(work.Name, work.Unit) == key);
            decimal enteredPrice;
            decimal? price = Money.TryParse(priceText, 2, out enteredPrice)
                ? (decimal?)enteredPrice
                : previous == null ? null : previous.Price;
            if (previous != null && previous.Name == name && previous.Unit == unit && previous.Price == price)
                return false;

            Json.WriteAtomic(path, new WorkTemplate {
                Name = name, Unit = unit, Price = price, UpdatedUtc = DateTime.UtcNow
            });
            return true;
        }

        private static string Key(string name, string unit)
        {
            return Regex.Replace(name.Trim(), @"\s+", " ").ToUpperInvariant() + "\0" +
                Regex.Replace(unit.Trim(), @"\s+", " ").ToUpperInvariant();
        }

        private static string FileName(string key)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "") + ".json";
        }
    }
}
