using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace InvoiceAndAct
{
    internal sealed class Documents
    {
        private const double Left = 38;
        private const double Right = PdfFile.Width - Left;
        private const double ContentWidth = Right - Left;
        private const double Bottom = 786;
        private const double BodySize = 10;
        private const double Leading = 13;
        private const double TableHeader = 27;

        private static readonly double[] Columns = { Left, 64, 305, 364, 416, 481, Right };
        private readonly Invoice invoice;
        private readonly bool act;
        private readonly PdfFile file = new PdfFile();
        private readonly string title;
        private PdfPage page;
        private double y;

        private Documents(Invoice invoice, bool act)
        {
            this.invoice = invoice;
            this.act = act;
            DateTime date = act ? invoice.ActDate : invoice.InvoiceDate;
            title = (act ? "Акт о сдаче-приёмке выполненных работ" : "Счёт на оплату") +
                " № " + invoice.Number + " от " + date.ToString("d MMMM yyyy 'г.'", Money.Russian);
        }

        public static void Write(Invoice invoice, bool act, string path)
        {
            var document = new Documents(invoice, act);
            document.Draw();
            document.file.Save(path);
        }

        private void Draw()
        {
            StartPage(true);
            if (!act)
                DrawBankDetails();

            double titleSize = act ? Math.Min(15, 15 * (ContentWidth - 1) / page.Measure(title, 15, true)) : 18;
            Paragraph(title, titleSize, act ? 20 : 22, true, 7);
            Horizontal(0.8, 0.2);
            y += 9;

            DrawParty(act ? "Исполнитель" : "Поставщик", invoice.Supplier.Party);
            DrawParty(act ? "Заказчик" : "Покупатель", invoice.Buyer);
            if (act)
            {
                y += 3;
                Paragraph("Акт составлен на основании счёта на оплату № " +
                    invoice.Number + " от " + ShortDate(invoice.InvoiceDate) + ".", BodySize, Leading, false, 3);
            }
            y += 9;

            DrawItems();
            DrawTotals();
            if (act)
                DrawActSignatures();
            else
                DrawInvoiceSignatures();

            for (int index = 0; index < file.Pages.Count; index++)
            {
                PdfPage current = file.Pages[index];
                current.Rule(Left, 805, Right, 805, 0.45, 0.7);
                current.Text((act ? "Акт" : "Счёт") + " № " + invoice.Number, Left, 818, 8, false);
                current.Text((index + 1) + " из " + file.Pages.Count, Right, 818, 8, false, TextAlign.Right);
            }
        }

        private static string ShortDate(DateTime date)
        {
            return date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        }

        private void StartPage(bool first)
        {
            page = file.AddPage();
            y = 36;
            if (!first)
            {
                Paragraph(title, 12, 16, true, 5);
                Horizontal(0.5, 0.65);
                y += 8;
            }
        }

        private void Ensure(double height)
        {
            if (y + height > Bottom)
                StartPage(false);
        }

        private void Paragraph(string text, double size, double leading, bool heavy, double after)
        {
            IList<string> lines = page.Wrap(text, ContentWidth, size, heavy);
            Ensure(lines.Count * leading + after);
            DrawLines(lines, Left, y, size, leading, heavy);
            y += lines.Count * leading + after;
        }

        private void DrawLines(IEnumerable<string> lines, double x, double top, double size, double leading, bool heavy)
        {
            double baseline = top + size;
            foreach (string line in lines)
            {
                page.Text(line, x, baseline, size, heavy);
                baseline += leading;
            }
        }

        private void Horizontal(double weight, double gray)
        {
            page.Rule(Left, y, Right, y, weight, gray);
        }

        private void DrawParty(string label, Party party)
        {
            const double labelWidth = 93;
            IList<string> value = page.Wrap(party.Description(), ContentWidth - labelWidth, BodySize, true);
            double height = Math.Max(1, value.Count) * Leading + 5;
            Ensure(height);
            page.Text(label, Left, y + BodySize, BodySize, false);
            DrawLines(value, Left + labelWidth, y, BodySize, Leading, true);
            y += height;
        }

        private void DrawBankDetails()
        {
            Supplier supplier = invoice.Supplier;
            Horizontal(0.7, 0.2);
            BankRow("Банк получателя", supplier.Bank, "БИК", supplier.Bic);
            BankRow("Получатель", supplier.Party.Name, "Расчётный счёт", supplier.Account);
            BankRow("ИНН / КПП", supplier.Party.TaxDetails(), "Корр. счёт", supplier.Correspondent);
            BankRow("Назначение платежа", "Оплата по счёту № " + invoice.Number + " от " +
                ShortDate(invoice.InvoiceDate), null, null);
            y += 16;
        }

        private void BankRow(string leftLabel, string leftValue, string rightLabel, string rightValue)
        {
            const double split = 350;
            double width = rightLabel == null ? ContentWidth : split - Left;
            IList<string> leftLines = page.Wrap(leftValue, width - 14, BodySize, false);
            IList<string> rightLines = page.Wrap(rightValue, Right - split - 14, BodySize, false);
            double height = 19 + Math.Max(1, Math.Max(leftLines.Count, rightLines.Count)) * Leading;
            page.Text(leftLabel, Left + 7, y + 11, 8, true);
            DrawLines(leftLines, Left + 7, y + 15, BodySize, Leading, false);
            if (rightLabel != null)
            {
                page.Rule(split, y, split, y + height, 0.5, 0.65);
                page.Text(rightLabel, split + 7, y + 11, 8, true);
                DrawLines(rightLines, split + 7, y + 15, BodySize, Leading, false);
            }
            page.Rule(Left, y, Left, y + height, 0.5, 0.65);
            page.Rule(Right, y, Right, y + height, 0.5, 0.65);
            y += height;
            Horizontal(0.5, 0.65);
        }

        private void DrawItems()
        {
            bool headerNeeded = true;
            int number = 0;
            foreach (LineItem item in invoice.Items)
            {
                number++;
                IList<string> description = page.Wrap(item.Name, Columns[2] - Columns[1] - 12, 9.5, false);
                IList<string> unit = page.Wrap(item.Unit, Columns[4] - Columns[3] - 10, 9, false);
                double rowHeight = Math.Max(description.Count, unit.Count) * 12 + 10;
                if (y + rowHeight + (headerNeeded ? TableHeader : 0) > Bottom)
                {
                    StartPage(false);
                    headerNeeded = true;
                }
                if (y + rowHeight + (headerNeeded ? TableHeader : 0) > Bottom)
                    throw new ArgumentException("Описание позиции " + number + " слишком длинное для одной страницы.");

                if (headerNeeded)
                {
                    DrawTableHeader();
                    headerNeeded = false;
                }
                page.Text(number.ToString(CultureInfo.InvariantCulture), (Columns[0] + Columns[1]) / 2,
                    y + 14, 9, false, TextAlign.Center);
                DrawLines(description, Columns[1] + 6, y + 4, 9.5, 12, false);
                TableNumber(Money.Quantity(item.Quantity), 2, false);
                double baseline = y + 14;
                foreach (string line in unit)
                {
                    page.Text(line, (Columns[3] + Columns[4]) / 2, baseline, 9, false, TextAlign.Center);
                    baseline += 12;
                }
                TableNumber(Money.Format(item.Price), 4, false);
                TableNumber(Money.Format(item.Amount), 5, false);
                TableFrame(rowHeight);
            }
        }

        private void DrawTableHeader()
        {
            page.Fill(Left, y, ContentWidth, TableHeader, 0.95);
            Horizontal(0.7, 0.35);
            string[] labels = { "№", act ? "Наименование работ (услуг)" : "Наименование товаров, работ, услуг",
                "Кол-во", "Ед. изм.", "Цена", "Сумма" };
            for (int index = 0; index < labels.Length; index++)
            {
                bool left = index == 1;
                double x = left ? Columns[index] + 6 : (Columns[index] + Columns[index + 1]) / 2;
                page.Text(labels[index], x, y + 17, 9, true, left ? TextAlign.Left : TextAlign.Center);
            }
            TableFrame(TableHeader);
        }

        private void TableNumber(string text, int column, bool heavy)
        {
            double width = Columns[column + 1] - Columns[column] - 10;
            double size = 9;
            double measured = page.Measure(text, size, heavy);
            if (measured > width)
                size *= width / measured;
            page.Text(text, Columns[column + 1] - 5, y + 14, size, heavy, TextAlign.Right);
        }

        private void TableFrame(double height)
        {
            foreach (double x in Columns)
                page.Rule(x, y, x, y + height, 0.45, 0.66);
            y += height;
            Horizontal(0.5, 0.66);
        }

        private void DrawTotals()
        {
            string total = Money.Format(invoice.Total);
            string vatLabel = "Без налога (НДС)";
            string vatValue = "-";
            if (invoice.VatRate.HasValue)
            {
                vatLabel = "В том числе НДС (" + Money.Quantity(invoice.VatRate.Value) + " %)";
                vatValue = Money.Format(invoice.VatAmount);
            }
            IList<string> words = page.Wrap(Money.InWords(invoice.Total) + ".", ContentWidth, BodySize, true);
            Ensure(104 + words.Count * Leading);
            y += 5;
            TotalRow("Итого", total, false);
            TotalRow(vatLabel, vatValue, false);
            TotalRow(act ? "Всего" : "Всего к оплате", total, true);

            y += 7;
            page.Text("Всего наименований " + invoice.Items.Count + " на сумму " + total + " руб.",
                Left, y + BodySize, BodySize, false);
            y += Leading + 2;
            DrawLines(words, Left, y, BodySize, Leading, true);
            y += words.Count * Leading + 6;
            Horizontal(0.7, 0.35);
            y += 8;
        }

        private void TotalRow(string label, string amount, bool heavy)
        {
            if (heavy)
                page.Fill(315, y, Right - 315, 21, 0.95);
            page.Text(label, Columns[5] - 9, y + 14, BodySize, heavy, TextAlign.Right);
            TableNumber(amount, 5, heavy);
            y += 21;
        }

        private void DrawInvoiceSignatures()
        {
            var primary = page.Wrap(invoice.Supplier.Party.Signatory, 233, BodySize, true);
            var accountant = page.Wrap(invoice.Supplier.Accountant, 233, BodySize, true);
            Ensure((Math.Max(1, primary.Count) + Math.Max(1, accountant.Count)) * Leading + 47);
            InvoiceSignature("Руководитель / ИП", primary);
            if (accountant.Count != 0)
                InvoiceSignature("Главный бухгалтер", accountant);
        }

        private void InvoiceSignature(string role, IList<string> name)
        {
            y += 12;
            page.Text(role, Left, y + BodySize, BodySize, false);
            page.Rule(157, y + 12, 298, y + 12, 0.55, 0.35);
            DrawLines(name, 314, y, BodySize, Leading, true);
            y += Math.Max(1, name.Count) * Leading + 8;
        }

        private void DrawActSignatures()
        {
            const double gap = 24;
            double columnWidth = (ContentWidth - gap) / 2;
            double rightStart = Left + columnWidth + gap;
            Supplier supplier = invoice.Supplier;
            Party buyer = invoice.Buyer;
            IList<string> left = PartyLines(supplier.Party, columnWidth, supplier);
            IList<string> right = PartyLines(buyer, columnWidth, null);
            IList<string> leftName = page.Wrap(supplier.Party.Signatory, columnWidth - 88, 9, true);
            IList<string> rightName = page.Wrap(buyer.Signatory, columnWidth - 88, 9, true);
            int nameHeight = Math.Max(1, Math.Max(leftName.Count, rightName.Count)) * 12;
            string acceptance = "Указанные работы (услуги) выполнены полностью и в срок. " +
                "Заказчик претензий по объёму, качеству и срокам выполнения работ (оказания услуг) не имеет.";
            int paragraphLines = page.Wrap(acceptance, ContentWidth, BodySize, false).Count;
            Ensure((paragraphLines + Math.Max(left.Count, right.Count)) * Leading + nameHeight + 70);

            Paragraph(acceptance, BodySize, Leading, false, 12);
            page.Text("Исполнитель", Left, y + 11, 11, true);
            page.Text("Заказчик", rightStart, y + 11, 11, true);
            y += 17;
            DrawLines(left, Left, y, 9, Leading, false);
            DrawLines(right, rightStart, y, 9, Leading, false);
            y += Math.Max(left.Count, right.Count) * Leading + 10;
            ActSignature(Left, columnWidth, leftName, nameHeight);
            ActSignature(rightStart, columnWidth, rightName, nameHeight);
        }

        private IList<string> PartyLines(Party party, double width, Supplier supplier)
        {
            var lines = new[] { party.Name, party.TaxDetails(), party.Address }.ToList();
            if (supplier != null)
            {
                lines.Add("Р/с " + supplier.Account);
                lines.Add(supplier.Bank);
                lines.Add("БИК " + supplier.Bic);
                lines.Add("К/с " + supplier.Correspondent);
            }
            return lines.SelectMany(line => page.Wrap(line, width, 9, false)).ToList();
        }

        private void ActSignature(double x, double width, IList<string> name, double nameHeight)
        {
            double nameStart = x + 84;
            double lineY = y + nameHeight + 1;
            DrawLines(name, nameStart + 3, y, 9, 12, true);
            page.Rule(x, lineY, x + 74, lineY, 0.55, 0.35);
            page.Rule(nameStart, lineY, x + width, lineY, 0.55, 0.35);
            page.Text("(подпись)", x + 37, lineY + 11, 8, false, TextAlign.Center);
            page.Text("(Ф. И. О.)", (nameStart + x + width) / 2, lineY + 11, 8, false, TextAlign.Center);
            page.Text("М. П.", x, lineY + 27, 8, false);
        }
    }
}
