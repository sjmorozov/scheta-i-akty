using System;
using System.Drawing;
using System.Windows.Forms;

namespace InvoiceAndAct
{
    internal sealed class SupplierForm : Form
    {
        private readonly SupplierSettings settings;
        private readonly TextBox name;
        private readonly TextBox inn;
        private readonly TextBox kpp;
        private readonly TextBox address;
        private readonly TextBox bank;
        private readonly TextBox bic;
        private readonly TextBox account;
        private readonly TextBox correspondent;
        private readonly TextBox signatory;
        private readonly TextBox accountant;

        public Supplier Value { get; private set; }

        public SupplierForm(Supplier supplier, SupplierSettings settings)
        {
            this.settings = settings;
            Text = "Реквизиты поставщика";
            Font = Ui.BaseFont;
            BackColor = Ui.Background;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            MinimumSize = new Size(660, 0);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            name = Ui.TextBox(200, supplier.Party.Name);
            inn = Ui.TextBox(12, supplier.Party.Inn);
            kpp = Ui.TextBox(9, supplier.Party.Kpp);
            address = Ui.TextBox(300, supplier.Party.Address);
            bank = Ui.TextBox(160, supplier.Bank);
            bic = Ui.TextBox(9, supplier.Bic);
            account = Ui.TextBox(20, supplier.Account);
            correspondent = Ui.TextBox(20, supplier.Correspondent);
            signatory = Ui.TextBox(100, supplier.Party.Signatory);
            accountant = Ui.TextBox(100, supplier.Accountant);

            var layout = new TableLayoutPanel {
                ColumnCount = 1, RowCount = 7, Dock = DockStyle.Fill,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(16, 13, 4, 15)
            };
            layout.Controls.Add(Ui.Field("Наименование или ФИО ИП", name), 0, 0);
            var taxes = Ui.Columns(58, 42);
            taxes.Controls.Add(Ui.Field("ИНН", inn), 0, 0);
            taxes.Controls.Add(Ui.Field("КПП (если есть)", kpp), 1, 0);
            layout.Controls.Add(taxes, 0, 1);
            layout.Controls.Add(Ui.Field("Адрес", address), 0, 2);
            layout.Controls.Add(Ui.Field("Банк получателя", bank), 0, 3);

            var accounts = Ui.Columns(24, 38, 38);
            accounts.Controls.Add(Ui.Field("БИК", bic), 0, 0);
            accounts.Controls.Add(Ui.Field("Расчётный счёт", account), 1, 0);
            accounts.Controls.Add(Ui.Field("Корр. счёт", correspondent), 2, 0);
            layout.Controls.Add(accounts, 0, 4);

            var signatures = Ui.Columns(50, 50);
            signatures.Controls.Add(Ui.Field("Подписант", signatory), 0, 0);
            signatures.Controls.Add(Ui.Field("Главный бухгалтер (если нужен)", accountant), 1, 0);
            layout.Controls.Add(signatures, 0, 5);

            var buttons = Ui.Buttons();
            buttons.Margin = new Padding(0, 8, 0, 0);
            var save = Ui.Button("Сохранить", true);
            var cancel = Ui.Button("Отмена");
            cancel.DialogResult = DialogResult.Cancel;
            save.Click += delegate { Save(); };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            layout.Controls.Add(buttons, 0, 6);
            Controls.Add(layout);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private void Save()
        {
            var supplier = new Supplier {
                Party = new Party {
                    Name = name.Text.Trim(), Inn = inn.Text.Trim(), Kpp = kpp.Text.Trim().ToUpperInvariant(),
                    Address = address.Text.Trim(), Signatory = signatory.Text.Trim()
                },
                Bank = bank.Text.Trim(), Bic = bic.Text.Trim(), Account = account.Text.Trim(),
                Correspondent = correspondent.Text.Trim(), Accountant = accountant.Text.Trim()
            };
            try
            {
                supplier.Validate();
                settings.Save(supplier);
                Value = supplier;
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Реквизиты поставщика", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
