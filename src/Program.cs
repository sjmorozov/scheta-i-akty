using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Счета и акты")]
[assembly: AssemblyProduct("Счета и акты")]
[assembly: AssemblyVersion("1.1.0.0")]

namespace InvoiceAndAct
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "InvoiceAndAct");

            AppDefaults defaults = AppDefaults.Load();
            var settings = new SupplierSettings(local);
            var history = new HistoryStore(Path.Combine(local, "Счета и акты"), defaults.FirstNumber, defaults.Buyers);
            Supplier supplier;
            string warning = "";
            try
            {
                supplier = settings.Load(defaults.Supplier);
            }
            catch (Exception ex)
            {
                supplier = defaults.Supplier ?? new Supplier();
                warning = "Не удалось прочитать сохранённые реквизиты поставщика. " + ex.Message;
            }

            var works = new WorkCatalog(Path.Combine(local, "Работы"));
            using (var form = new MainForm(history, settings, supplier, works))
            {
                if (warning.Length > 0)
                {
                    form.Shown += delegate {
                        MessageBox.Show(form, warning, "Реквизиты поставщика", MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    };
                }
                Application.Run(form);
            }
        }
    }
}
