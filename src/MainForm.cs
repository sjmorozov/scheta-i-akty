using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace InvoiceAndAct
{
    internal sealed class MainForm : Form
    {
        private readonly HistoryStore history;
        private readonly SupplierSettings settings;
        private Supplier supplier;
        private readonly TabControl tabs = new TabControl();
        private readonly TabPage composeTab = new TabPage("Новый счёт");
        private readonly TabPage historyTab = new TabPage("История");
        private readonly Label supplierCaption = Ui.Label("");
        private readonly Label total = Ui.Label("");
        private readonly Label status = Ui.Label(" ");
        private readonly TextBox number = Ui.TextBox(9);
        private readonly DateTimePicker invoiceDate = DatePicker();
        private readonly DateTimePicker actDate = DatePicker();
        private readonly ComboBox vatMode = Combo();
        private readonly TextBox vatPercent = Ui.TextBox(6);
        private readonly ComboBox savedBuyers = Combo();
        private readonly TextBox buyerName = Ui.TextBox(200);
        private readonly TextBox buyerInn = Ui.TextBox(12);
        private readonly TextBox buyerKpp = Ui.TextBox(9);
        private readonly TextBox buyerAddress = Ui.TextBox(300);
        private readonly TextBox buyerSignatory = Ui.TextBox(100);
        private readonly DataGridView items = Ui.Grid();
        private readonly DataGridView records = Ui.Grid();
        private readonly Button createPair = Ui.Button("Создать счёт и акт", true);
        private readonly Button openLast = Ui.Button("Открыть папку с PDF");
        private readonly Button openInvoice = Ui.Button("Открыть счёт");
        private readonly Button openAct = Ui.Button("Открыть акт");
        private readonly Button openFolder = Ui.Button("Открыть папку");
        private IList<Party> buyers = new List<Party>();
        private string suggestedNumber = "";
        private DateTime previousInvoiceDate = DateTime.Today;
        private bool updatingBuyers;
        private int lastSavedNumber;

        public MainForm(HistoryStore history, SupplierSettings settings, Supplier supplier)
        {
            this.history = history;
            this.settings = settings;
            this.supplier = supplier;
            Text = "Счета и акты";
            Font = Ui.BaseFont;
            BackColor = Ui.Background;
            ForeColor = Ui.Ink;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1000, 680);
            MinimumSize = new Size(850, 650);

            var layout = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, Dock = DockStyle.Fill };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(BuildHeader(), 0, 0);

            tabs.Dock = DockStyle.Fill;
            tabs.Margin = new Padding(12, 10, 12, 10);
            tabs.TabPages.Add(composeTab);
            tabs.TabPages.Add(historyTab);
            composeTab.BackColor = Ui.Background;
            historyTab.BackColor = Ui.Background;
            composeTab.Controls.Add(BuildCompose());
            historyTab.Controls.Add(BuildHistory());
            tabs.SelectedIndexChanged += delegate { RefreshHistorySafely(false); };
            layout.Controls.Add(tabs, 0, 1);
            Controls.Add(layout);

            UpdateSupplierCaption();
            ResetForm();
            RefreshHistorySafely(true);
        }

        private Control BuildHeader()
        {
            var header = new TableLayoutPanel {
                ColumnCount = 2, RowCount = 1, Dock = DockStyle.Fill, Margin = Padding.Empty,
                Padding = new Padding(18, 9, 14, 8), BackColor = Color.White
            };
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var names = new TableLayoutPanel {
                ColumnCount = 1, RowCount = 2, Dock = DockStyle.Fill, Margin = Padding.Empty
            };
            names.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            names.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var heading = Ui.Label("Счета и акты");
            heading.Font = Ui.TitleFont;
            supplierCaption.AutoSize = false;
            supplierCaption.AutoEllipsis = true;
            supplierCaption.Dock = DockStyle.Fill;
            supplierCaption.ForeColor = Ui.Muted;
            names.Controls.Add(heading, 0, 0);
            names.Controls.Add(supplierCaption, 0, 1);
            header.Controls.Add(names, 0, 0);

            var edit = Ui.Button("Реквизиты поставщика");
            edit.Anchor = AnchorStyles.Right;
            edit.Click += delegate { EditSupplier(); };
            header.Controls.Add(edit, 1, 0);
            return header;
        }

        private Control BuildCompose()
        {
            var compose = new TableLayoutPanel {
                ColumnCount = 1, RowCount = 5, Dock = DockStyle.Fill, Padding = new Padding(13, 12, 10, 8)
            };
            compose.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            compose.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            compose.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            compose.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            compose.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            compose.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            compose.Controls.Add(BuildDocumentFields(), 0, 0);
            compose.Controls.Add(BuildBuyerFields(), 0, 1);
            compose.Controls.Add(BuildItems(), 0, 2);
            compose.Controls.Add(BuildActions(), 0, 3);
            status.Dock = DockStyle.Fill;
            status.Margin = new Padding(1, 3, 0, 0);
            status.ForeColor = Ui.Muted;
            status.MinimumSize = new Size(0, 19);
            compose.Controls.Add(status, 0, 4);
            return compose;
        }

        private Control BuildDocumentFields()
        {
            var fields = Ui.Columns(15, 21, 21, 43);
            fields.Controls.Add(Ui.Field("Номер счёта", number), 0, 0);
            fields.Controls.Add(Ui.Field("Дата счёта", invoiceDate), 1, 0);
            fields.Controls.Add(Ui.Field("Дата акта", actDate), 2, 0);
            invoiceDate.ValueChanged += delegate {
                if (actDate.Value.Date == previousInvoiceDate)
                    actDate.Value = invoiceDate.Value.Date;
                previousInvoiceDate = invoiceDate.Value.Date;
            };

            var vat = Ui.Columns(70, 30);
            vatMode.Items.AddRange(new object[] { "Без НДС", "Включён в цену" });
            vatMode.Dock = DockStyle.Top;
            vatMode.Margin = new Padding(0, 0, 7, 0);
            vatPercent.Dock = DockStyle.Top;
            vatPercent.Margin = Padding.Empty;
            vatPercent.AccessibleName = "Ставка НДС в процентах";
            vatPercent.Enabled = false;
            vatMode.SelectedIndexChanged += delegate { vatPercent.Enabled = vatMode.SelectedIndex == 1; };
            vatMode.SelectedIndex = 0;
            vat.Controls.Add(vatMode, 0, 0);
            vat.Controls.Add(vatPercent, 1, 0);
            fields.Controls.Add(Ui.Field("НДС и ставка, %", vat), 3, 0);
            return fields;
        }

        private Control BuildBuyerFields()
        {
            var group = Ui.Columns(100);
            group.RowCount = 2;
            var first = Ui.Columns(35, 65);
            first.Controls.Add(Ui.Field("Сохранённые покупатели", savedBuyers), 0, 0);
            first.Controls.Add(Ui.Field("Покупатель / заказчик", buyerName), 1, 0);
            group.Controls.Add(first, 0, 0);

            var details = Ui.Columns(15, 13, 39, 33);
            details.Controls.Add(Ui.Field("ИНН", buyerInn), 0, 0);
            details.Controls.Add(Ui.Field("КПП", buyerKpp), 1, 0);
            details.Controls.Add(Ui.Field("Адрес", buyerAddress), 2, 0);
            details.Controls.Add(Ui.Field("Подписант акта", buyerSignatory), 3, 0);
            group.Controls.Add(details, 0, 1);
            savedBuyers.SelectedIndexChanged += delegate {
                if (!updatingBuyers)
                    FillBuyer(savedBuyers.SelectedIndex > 0 ? buyers[savedBuyers.SelectedIndex - 1] : new Party());
            };
            return group;
        }

        private Control BuildItems()
        {
            var group = new TableLayoutPanel {
                ColumnCount = 1, RowCount = 2, Dock = DockStyle.Fill, Margin = new Padding(0, 4, 12, 7)
            };
            group.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            group.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var toolbar = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Top };
            toolbar.Margin = new Padding(0, 0, 0, 6);
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var caption = Ui.Label("Позиции");
            caption.Font = Ui.BoldFont;
            caption.Anchor = AnchorStyles.Left;
            toolbar.Controls.Add(caption, 0, 0);
            var actions = Ui.Buttons();
            var add = Ui.Button("Добавить строку");
            var remove = Ui.Button("Удалить строку");
            remove.Margin = Padding.Empty;
            add.Click += delegate { AddItem(true); };
            remove.Click += delegate {
                if (items.CurrentRow != null)
                    items.Rows.Remove(items.CurrentRow);
                if (items.Rows.Count == 0)
                    AddItem(false);
                RefreshAmounts();
            };
            actions.Controls.Add(add);
            actions.Controls.Add(remove);
            toolbar.Controls.Add(actions, 1, 0);

            items.AccessibleName = "Позиции счёта";
            items.Margin = Padding.Empty;
            items.EditMode = DataGridViewEditMode.EditOnEnter;
            items.Columns.AddRange(
                Ui.Column("Наименование", 50, 200, 1200),
                Ui.Column("Кол-во", 11, 82, 24, true),
                Ui.Column("Ед. изм.", 10, 84, 20),
                Ui.Column("Цена, руб.", 14, 105, 24, true),
                Ui.Column("Сумма, руб.", 15, 120, 24, true));
            items.Columns[4].ReadOnly = true;
            items.Columns[4].DefaultCellStyle.BackColor = Color.FromArgb(239, 243, 249);
            items.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e) {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && e.ColumnIndex < 4)
                    RefreshAmounts();
            };
            group.Controls.Add(toolbar, 0, 0);
            group.Controls.Add(items, 0, 1);
            return group;
        }

        private Control BuildActions()
        {
            var footer = new TableLayoutPanel {
                ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Top,
                Margin = new Padding(0, 3, 12, 1)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            total.Font = Ui.BoldFont;
            total.Anchor = AnchorStyles.Left;
            footer.Controls.Add(total, 0, 0);

            var actions = Ui.Buttons();
            openLast.Enabled = false;
            openLast.Click += delegate { OpenPath(history.FolderFor(lastSavedNumber), true); };
            createPair.Margin = Padding.Empty;
            createPair.Click += delegate { SavePair(); };
            actions.Controls.Add(openLast);
            actions.Controls.Add(createPair);
            footer.Controls.Add(actions, 1, 0);
            return footer;
        }

        private Control BuildHistory()
        {
            var view = new TableLayoutPanel {
                ColumnCount = 1, RowCount = 2, Dock = DockStyle.Fill, Padding = new Padding(13, 12, 13, 10)
            };
            view.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            view.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            records.AccessibleName = "История счетов";
            records.Margin = new Padding(0, 0, 0, 9);
            records.ReadOnly = true;
            records.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            records.Columns.AddRange(Ui.Column("Номер", 10, 80, 9), Ui.Column("Дата счёта", 15, 115, 10),
                Ui.Column("Дата акта", 15, 115, 10), Ui.Column("Покупатель", 44, 190, 200),
                Ui.Column("Сумма, руб.", 16, 120, 24, true));
            records.SelectionChanged += delegate {
                openInvoice.Enabled = openAct.Enabled = openFolder.Enabled = SelectedEntry() != null;
            };
            records.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) {
                if (e.RowIndex >= 0)
                    OpenSelected(false);
            };
            view.Controls.Add(records, 0, 0);

            var buttons = Ui.Buttons();
            openInvoice.Enabled = openAct.Enabled = openFolder.Enabled = false;
            openInvoice.Click += delegate { OpenSelected(false); };
            openAct.Click += delegate { OpenSelected(true); };
            openFolder.Click += delegate {
                HistoryEntry entry = SelectedEntry();
                if (entry != null)
                    OpenPath(history.FolderFor(entry.Number), true);
            };
            buttons.Controls.Add(openInvoice);
            buttons.Controls.Add(openAct);
            buttons.Controls.Add(openFolder);
            view.Controls.Add(buttons, 0, 1);
            return view;
        }

        private static DateTimePicker DatePicker()
        {
            return new DateTimePicker { Width = 120, Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy" };
        }

        private static ComboBox Combo()
        {
            return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 530, MaxDropDownItems = 12 };
        }

        private void UpdateSupplierCaption()
        {
            string name = supplier.Party.Name;
            const string entrepreneur = "Индивидуальный предприниматель ";
            if (string.IsNullOrWhiteSpace(name))
                name = "Заполни реквизиты поставщика";
            else if (name.StartsWith(entrepreneur, StringComparison.OrdinalIgnoreCase))
                name = "ИП " + name.Substring(entrepreneur.Length);
            supplierCaption.Text = name;
        }

        private bool EditSupplier()
        {
            using (var editor = new SupplierForm(supplier, settings))
            {
                if (editor.ShowDialog(this) != DialogResult.OK)
                    return false;
                supplier = editor.Value;
                UpdateSupplierCaption();
                return true;
            }
        }

        private void FillBuyer(Party buyer)
        {
            buyerName.Text = buyer.Name ?? "";
            buyerInn.Text = buyer.Inn ?? "";
            buyerKpp.Text = buyer.Kpp ?? "";
            buyerAddress.Text = buyer.Address ?? "";
            buyerSignatory.Text = buyer.Signatory ?? "";
        }

        private void AddItem(bool edit)
        {
            int row = items.Rows.Add("", "1", "", "", "");
            if (edit)
            {
                items.CurrentCell = items.Rows[row].Cells[0];
                items.Focus();
                items.BeginEdit(true);
            }
        }

        private static string Cell(DataGridViewRow row, int column)
        {
            return Convert.ToString(row.Cells[column].Value, CultureInfo.InvariantCulture).Trim();
        }

        private static bool Blank(DataGridViewRow row)
        {
            string quantity = Cell(row, 1);
            return Cell(row, 0).Length == 0 && Cell(row, 2).Length == 0 && Cell(row, 3).Length == 0 &&
                (quantity.Length == 0 || quantity == "1");
        }

        private static LineItem ReadItem(DataGridViewRow row)
        {
            return new LineItem(Cell(row, 0), Cell(row, 2), Cell(row, 1), Cell(row, 3), row.Index + 1);
        }

        private void RefreshAmounts()
        {
            decimal sum = 0;
            bool complete = true;
            foreach (DataGridViewRow row in items.Rows)
            {
                string amount = "";
                if (!Blank(row))
                {
                    try
                    {
                        LineItem item = ReadItem(row);
                        sum += item.Amount;
                        amount = Money.Format(item.Amount);
                    }
                    catch (ArgumentException)
                    {
                        complete = false;
                    }
                }
                if (Cell(row, 4) != amount)
                    row.Cells[4].Value = amount;
            }
            total.Text = complete ? "К оплате " + Money.Format(sum) + " ₽" : "К оплате —";
        }

        private decimal? ReadVat()
        {
            if (vatMode.SelectedIndex != 1)
                return null;
            decimal rate;
            if (!decimal.TryParse(vatPercent.Text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out rate) || rate > 100 || rate != decimal.Round(rate, 2))
                throw new ArgumentException("Укажи ставку НДС в процентах или выбери «Без НДС».");
            return rate;
        }

        private void SavePair()
        {
            if (string.IsNullOrWhiteSpace(supplier.Party.Name) && !EditSupplier())
                return;
            createPair.Enabled = false;
            Invoice invoice;
            try
            {
                items.EndEdit();
                int value;
                if (!int.TryParse(number.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value))
                    throw new ArgumentException("Укажи номер счёта цифрами.");
                var buyer = new Party {
                    Name = buyerName.Text.Trim(), Inn = buyerInn.Text.Trim(), Kpp = buyerKpp.Text.Trim().ToUpperInvariant(),
                    Address = buyerAddress.Text.Trim(), Signatory = buyerSignatory.Text.Trim()
                };
                var lines = items.Rows.Cast<DataGridViewRow>().Where(row => !Blank(row)).Select(ReadItem);
                invoice = new Invoice(value, invoiceDate.Value, actDate.Value, supplier, buyer, lines, ReadVat());
                history.Save(invoice);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Счёт и акт", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            finally
            {
                createPair.Enabled = true;
            }

            lastSavedNumber = invoice.Number;
            openLast.Enabled = true;
            status.Text = "Счёт № " + invoice.Number + " и акт сохранены.";
            ResetForm();
            RefreshHistorySafely(true);
            buyerName.Focus();
        }

        private void ResetForm()
        {
            savedBuyers.SelectedIndex = savedBuyers.Items.Count == 0 ? -1 : 0;
            FillBuyer(new Party());
            vatMode.SelectedIndex = 0;
            vatPercent.Clear();
            items.Rows.Clear();
            AddItem(false);
            RefreshAmounts();
        }

        private void RefreshHistorySafely(bool resetNumber)
        {
            try
            {
                IList<HistoryEntry> entries = history.Read();
                RefreshBuyerList(HistoryStore.Buyers(entries));
                RefreshRecordList(entries);
                string next = history.SuggestNumber(entries);
                if (resetNumber || number.Text.Trim() == suggestedNumber)
                    number.Text = next;
                suggestedNumber = next;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось прочитать историю. " + ex.Message, "История счетов",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RefreshBuyerList(IList<Party> updated)
        {
            Party selected = savedBuyers.SelectedIndex > 0 ? buyers[savedBuyers.SelectedIndex - 1] : null;
            buyers = updated;
            updatingBuyers = true;
            savedBuyers.BeginUpdate();
            try
            {
                savedBuyers.Items.Clear();
                savedBuyers.Items.Add("Новый покупатель");
                int selectedIndex = 0;
                for (int i = 0; i < buyers.Count; i++)
                {
                    Party buyer = buyers[i];
                    string caption = buyer.Name;
                    if (!string.IsNullOrEmpty(buyer.Inn))
                        caption += "  ·  ИНН " + buyer.Inn;
                    savedBuyers.Items.Add(caption);
                    if (selected != null && buyer.Inn == selected.Inn && buyer.Name == selected.Name)
                        selectedIndex = i + 1;
                }
                savedBuyers.SelectedIndex = selectedIndex;
            }
            finally
            {
                savedBuyers.EndUpdate();
                updatingBuyers = false;
            }
        }

        private void RefreshRecordList(IEnumerable<HistoryEntry> entries)
        {
            records.Rows.Clear();
            foreach (HistoryEntry entry in entries)
            {
                int row = records.Rows.Add(entry.Number, entry.InvoiceDate, entry.ActDate,
                    entry.Buyer == null ? "Запись недоступна" : entry.Buyer.Name,
                    entry.Buyer == null ? "" : Money.Format(entry.Total));
                records.Rows[row].Tag = entry;
            }
            if (records.Rows.Count > 0)
            {
                records.CurrentCell = records.Rows[0].Cells[0];
                openInvoice.Enabled = openAct.Enabled = openFolder.Enabled = true;
            }
        }

        private HistoryEntry SelectedEntry()
        {
            return records.CurrentRow == null ? null : records.CurrentRow.Tag as HistoryEntry;
        }

        private void OpenSelected(bool act)
        {
            HistoryEntry entry = SelectedEntry();
            if (entry != null)
                OpenPath(history.FileFor(entry.Number, act), false);
        }

        private void OpenPath(string path, bool folder)
        {
            try
            {
                if (folder ? !Directory.Exists(path) : !File.Exists(path))
                    throw new IOException(folder ? "Папка не найдена." : "PDF не найден.");
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Документы", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
