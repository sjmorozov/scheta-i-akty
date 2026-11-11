using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;

namespace InvoiceAndAct
{
    [DataContract]
    internal sealed class AppDefaults
    {
        [DataMember] public int FirstNumber { get; set; }
        [DataMember] public Supplier Supplier { get; set; }
        [DataMember] public Party[] Buyers { get; set; }

        public static AppDefaults Load()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("defaults.json"))
                return stream == null ? new AppDefaults { FirstNumber = 1 } : Json.Read<AppDefaults>(stream);
        }
    }

    internal static class Json
    {
        public static T Read<T>(Stream stream)
        {
            return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }

        public static T Read<T>(string path)
        {
            using (FileStream stream = File.OpenRead(path))
                return Read<T>(stream);
        }

        public static void WriteAtomic<T>(string path, T value)
        {
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                Write(temporary, value);
                if (File.Exists(path))
                {
                    File.Replace(temporary, path, null);
                    return;
                }
                try
                {
                    File.Move(temporary, path);
                }
                catch (IOException)
                {
                    if (!File.Exists(path))
                        throw;
                    File.Replace(temporary, path, null);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        public static void Write<T>(string path, T value)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew))
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                stream.Flush(true);
            }
        }
    }

    internal sealed class SupplierSettings
    {
        private readonly string path;

        public SupplierSettings(string directory)
        {
            path = Path.Combine(directory, "supplier.json");
        }

        public Supplier Load(Supplier fallback)
        {
            Supplier supplier = File.Exists(path) ? Json.Read<Supplier>(path) : fallback;
            return supplier != null && supplier.Party != null ? supplier : new Supplier();
        }

        public void Save(Supplier supplier)
        {
            Json.WriteAtomic(path, supplier);
        }
    }

    [DataContract]
    internal sealed class HistoryEntry
    {
        [DataMember] public int Number { get; set; }
        [DataMember] public string InvoiceDate { get; set; }
        [DataMember] public string ActDate { get; set; }
        [DataMember] public Party Buyer { get; set; }
        [DataMember] public decimal Total { get; set; }
        [DataMember] public DateTime SavedUtc { get; set; }
    }

    internal sealed class HistoryStore
    {
        private const string FolderPrefix = "Счёт № ";
        private const string RecordFile = "Запись.json";
        private readonly string root;
        private readonly int firstNumber;
        private readonly Party[] initialBuyers;

        public HistoryStore(string directory, int firstNumber, Party[] initialBuyers = null)
        {
            root = Path.GetFullPath(directory);
            this.firstNumber = firstNumber > 0 && firstNumber <= Invoice.MaxNumber ? firstNumber : 1;
            this.initialBuyers = initialBuyers ?? new Party[0];
        }

        public IList<HistoryEntry> Read()
        {
            var history = new List<HistoryEntry>();
            if (!Directory.Exists(root))
                return history;

            foreach (string directory in Directory.EnumerateDirectories(root, FolderPrefix + "*"))
            {
                string name = Path.GetFileName(directory);
                int number;
                if (!name.StartsWith(FolderPrefix, StringComparison.Ordinal) ||
                    !int.TryParse(name.Substring(FolderPrefix.Length), NumberStyles.None,
                        CultureInfo.InvariantCulture, out number) ||
                    number < 1 || number > Invoice.MaxNumber || name != FolderName(number))
                    continue;

                HistoryEntry entry = ReadEntry(directory, number);
                history.Add(entry);
            }
            return history.OrderByDescending(entry => entry.Number).ToList();
        }

        private static HistoryEntry ReadEntry(string directory, int number)
        {
            try
            {
                HistoryEntry entry = Json.Read<HistoryEntry>(Path.Combine(directory, RecordFile));
                if (entry != null && entry.Number == number && entry.Buyer != null)
                    return entry;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (SerializationException) { }

            // Even an unreadable record reserves its number.
            return new HistoryEntry { Number = number };
        }

        public string SuggestNumber(IList<HistoryEntry> entries)
        {
            if (entries.Count == 0)
                return firstNumber.ToString(CultureInfo.InvariantCulture);
            int last = entries.Max(entry => entry.Number);
            return last < Invoice.MaxNumber ? (last + 1).ToString(CultureInfo.InvariantCulture) : "";
        }

        public IList<Party> Buyers(IList<HistoryEntry> entries)
        {
            return entries.OrderByDescending(entry => entry.SavedUtc)
                .Select(entry => entry.Buyer)
                .Concat(initialBuyers)
                .Where(buyer => buyer != null && !string.IsNullOrWhiteSpace(buyer.Name))
                .GroupBy(BuyerKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(buyer => buyer.Name, StringComparer.Create(Money.Russian, true))
                .ToList();
        }

        private static string BuyerKey(Party buyer)
        {
            return string.IsNullOrWhiteSpace(buyer.Inn)
                ? "name:" + Regex.Replace(buyer.Name.Trim(), @"\s+", " ")
                : "inn:" + buyer.Inn;
        }

        public string FolderFor(int number)
        {
            return Path.Combine(root, FolderName(number));
        }

        public string FileFor(int number, bool act)
        {
            return Path.Combine(FolderFor(number), PdfName(number, act));
        }

        private static string PdfName(int number, bool act)
        {
            return (act ? "Акт № " : "Счёт № ") + number.ToString(CultureInfo.InvariantCulture) + ".pdf";
        }

        private static string FolderName(int number)
        {
            return FolderPrefix + number.ToString(CultureInfo.InvariantCulture);
        }

        public void Save(Invoice invoice)
        {
            Directory.CreateDirectory(root);
            string target = FolderFor(invoice.Number);
            CheckNumberIsFree(target, invoice.Number);
            string temporary = Path.Combine(root, ".tmp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            string invoiceFile = PdfName(invoice.Number, false);
            string actFile = PdfName(invoice.Number, true);
            try
            {
                Documents.Write(invoice, false, Path.Combine(temporary, invoiceFile));
                Documents.Write(invoice, true, Path.Combine(temporary, actFile));
                Json.Write(Path.Combine(temporary, RecordFile), new HistoryEntry {
                    Number = invoice.Number,
                    InvoiceDate = invoice.InvoiceDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    ActDate = invoice.ActDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    Buyer = invoice.Buyer,
                    Total = invoice.Total,
                    SavedUtc = DateTime.UtcNow
                });

                // The pair and its history entry become visible in the same rename.
                try
                {
                    Directory.Move(temporary, target);
                }
                catch (IOException)
                {
                    CheckNumberIsFree(target, invoice.Number);
                    throw;
                }
            }
            finally
            {
                if (Directory.Exists(temporary))
                {
                    foreach (string file in new[] { invoiceFile, actFile, RecordFile })
                        File.Delete(Path.Combine(temporary, file));
                    Directory.Delete(temporary);
                }
            }
        }

        private void CheckNumberIsFree(string path, int number)
        {
            if (Directory.Exists(path) || File.Exists(path))
                throw new InvalidOperationException("Счёт № " + number +
                    " уже сохранён. Свободный номер по истории " + SuggestNumber(Read()) + ".");
        }
    }
}
