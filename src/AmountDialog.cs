using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace InvoiceAndAct
{
    internal sealed class AmountDialog : Form
    {
        private readonly decimal otherTotal;
        private readonly TextBox target = Ui.TextBox(24);
        private readonly TextBox fixedValue = Ui.TextBox(24);
        private readonly ComboBox mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly DataGridView options = Ui.Grid();
        private readonly Label message = Ui.Label(" ");
        private readonly Button apply = Ui.Button("Применить к строке", true);

        public AmountOption SelectedOption { get; private set; }

        public AmountDialog(decimal otherTotal, decimal? currentTotal, string unit)
        {
            this.otherTotal = otherTotal;
            Text = "Подобрать количество и цену";
            Font = Ui.BaseFont;
            BackColor = Ui.Background;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(730, 506);
            MinimumSize = new Size(670, 435);
            MinimizeBox = false;
            ShowInTaskbar = false;

            var layout = new TableLayoutPanel {
                ColumnCount = 1, RowCount = 5, Dock = DockStyle.Fill, Padding = new Padding(13, 13, 13, 12)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var fields = Ui.Columns(31, 34, 35);
            mode.Items.AddRange(new object[] { "По количеству", "По цене за единицу" });
            mode.SelectedIndex = 0;
            fields.Controls.Add(Ui.Field("Сумма счёта, руб.", target), 0, 0);
            fields.Controls.Add(Ui.Field("Подбор", mode), 1, 0);
            fields.Controls.Add(Ui.Field("Задать значение (по желанию)", fixedValue), 2, 0);
            layout.Controls.Add(fields, 0, 0);
            if (currentTotal.HasValue && currentTotal.Value > otherTotal)
                target.Text = Money.Format(currentTotal.Value);

            var toolbar = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Dock = DockStyle.Top, AutoSize = true };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.Margin = new Padding(0, 0, 0, 5);
            var other = Ui.Label("По остальным строкам " + Money.Format(otherTotal) + " ₽");
            other.Anchor = AnchorStyles.Left;
            other.ForeColor = Ui.Muted;
            toolbar.Controls.Add(other, 0, 0);
            var calculate = Ui.Button("Показать варианты");
            calculate.Margin = Padding.Empty;
            calculate.Click += delegate { ShowOptions(); };
            toolbar.Controls.Add(calculate, 1, 0);
            layout.Controls.Add(toolbar, 0, 1);

            message.MinimumSize = new Size(0, 21);
            message.Margin = new Padding(0, 2, 0, 3);
            message.ForeColor = Ui.Muted;
            layout.Controls.Add(message, 0, 2);

            options.Margin = Padding.Empty;
            options.ReadOnly = true;
            options.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            options.AccessibleName = "Варианты количества и цены";
            string quantityTitle = unit.Length == 0 ? "Количество" : "Количество, " + unit;
            options.Columns.AddRange(OptionColumn(quantityTitle, "0.######"),
                OptionColumn("Цена за единицу, руб.", "N2"));
            options.SelectionChanged += delegate { apply.Enabled = CurrentOption() != null; };
            options.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) {
                if (e.RowIndex >= 0)
                    ApplyOption();
            };
            layout.Controls.Add(options, 0, 3);

            var buttons = Ui.Buttons();
            buttons.Margin = new Padding(0, 9, 0, 0);
            apply.Enabled = false;
            apply.Click += delegate { ApplyOption(); };
            var cancel = Ui.Button("Отмена");
            cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(apply);
            buttons.Controls.Add(cancel);
            layout.Controls.Add(buttons, 0, 4);
            Controls.Add(layout);

            target.TextChanged += delegate { ClearOptions(); };
            fixedValue.TextChanged += delegate { ClearOptions(); };
            mode.SelectedIndexChanged += delegate {
                fixedValue.Clear();
                ClearOptions();
            };
            AcceptButton = calculate;
            CancelButton = cancel;
            ActiveControl = target;
        }

        private static DataGridViewTextBoxColumn OptionColumn(string label, string format)
        {
            var column = Ui.Column(label, 50, 130, 24, true);
            column.SortMode = DataGridViewColumnSortMode.Automatic;
            column.ValueType = typeof(decimal);
            column.DefaultCellStyle.Format = format;
            column.DefaultCellStyle.FormatProvider = Money.Russian;
            return column;
        }

        private void ShowOptions()
        {
            ClearOptions();
            decimal total;
            if (!Money.TryParse(target.Text, 2, out total) || total <= 0)
            {
                SetMessage("Укажи положительную сумму счёта с точностью до копейки.", true);
                return;
            }
            decimal amount = total - otherTotal;
            if (amount <= 0)
            {
                SetMessage("Сумма счёта должна быть больше суммы остальных строк.", true);
                return;
            }

            bool byQuantity = mode.SelectedIndex == 0;
            decimal? known = null;
            if (!string.IsNullOrWhiteSpace(fixedValue.Text))
            {
                decimal value;
                if (!Money.TryParse(fixedValue.Text, byQuantity ? 6 : 2, out value) || value <= 0)
                {
                    SetMessage(byQuantity ? "Укажи положительное количество, до 6 знаков после запятой."
                        : "Укажи положительную цену с точностью до копейки.", true);
                    return;
                }
                known = value;
            }

            IList<AmountOption> variants = AmountOptions.Find(amount, byQuantity, known);
            if (variants.Count == 0)
            {
                SetMessage("С таким значением нужная сумма не получается с точностью до копейки.", true);
                return;
            }

            options.Columns[byQuantity ? 0 : 1].DisplayIndex = 0;
            foreach (AmountOption variant in variants)
            {
                int row = options.Rows.Add(variant.Quantity, variant.Price);
                options.Rows[row].Tag = variant;
            }
            options.CurrentCell = options.Rows[0].Cells[byQuantity ? 0 : 1];
            apply.Enabled = true;
            SetMessage("Сумма выбранной строки " + Money.Format(amount) + " ₽", false);
        }

        private void ClearOptions()
        {
            options.Rows.Clear();
            apply.Enabled = false;
            SetMessage(" ", false);
        }

        private void SetMessage(string text, bool error)
        {
            message.Text = text;
            message.ForeColor = error ? Color.Firebrick : Ui.Muted;
        }

        private AmountOption CurrentOption()
        {
            return options.CurrentRow == null ? null : options.CurrentRow.Tag as AmountOption;
        }

        private void ApplyOption()
        {
            SelectedOption = CurrentOption();
            if (SelectedOption != null)
                DialogResult = DialogResult.OK;
        }
    }
}
