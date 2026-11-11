using System.Drawing;
using System.Windows.Forms;

namespace InvoiceAndAct
{
    internal static class Ui
    {
        public static readonly Color Background = Color.FromArgb(244, 246, 250);
        public static readonly Color Ink = Color.FromArgb(27, 39, 56);
        public static readonly Color Muted = Color.FromArgb(79, 97, 119);
        public static readonly Color Accent = Color.FromArgb(28, 80, 170);
        public static readonly Color Line = Color.FromArgb(218, 225, 235);
        public static readonly Font BaseFont = new Font("Segoe UI", 10);
        public static readonly Font BoldFont = new Font(BaseFont, FontStyle.Bold);
        public static readonly Font TitleFont = new Font("Segoe UI", 17, FontStyle.Bold);

        public static TableLayoutPanel Columns(params float[] widths)
        {
            var panel = new TableLayoutPanel {
                ColumnCount = widths.Length,
                RowCount = 1,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = Padding.Empty,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };
            foreach (float width in widths)
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
            return panel;
        }

        public static Label Label(string text)
        {
            return new Label {
                Text = text,
                AutoSize = true,
                ForeColor = Ink,
                Margin = Padding.Empty
            };
        }

        public static Control Field(string label, Control input)
        {
            input.AccessibleName = label;
            input.Dock = DockStyle.Top;
            input.Margin = Padding.Empty;
            var layout = new TableLayoutPanel {
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 12, 8)
            };
            var caption = Label(label);
            caption.Margin = new Padding(0, 0, 0, 3);
            layout.Controls.Add(caption, 0, 0);
            layout.Controls.Add(input, 0, 1);
            return layout;
        }

        public static TextBox TextBox(int maxLength, string value = null)
        {
            return new TextBox { MaxLength = maxLength, Text = value ?? "" };
        }

        public static Button Button(string text, bool primary = false)
        {
            var button = new Button {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(0, 30),
                Padding = new Padding(12, 0, 12, 0),
                Margin = new Padding(0, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = primary ? Accent : Color.White,
                ForeColor = primary ? Color.White : Ink
            };
            button.FlatAppearance.BorderColor = primary ? Accent : Line;
            return button;
        }

        public static FlowLayoutPanel Buttons()
        {
            return new FlowLayoutPanel {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Dock = DockStyle.Top,
                Margin = Padding.Empty
            };
        }

        public static DataGridView Grid()
        {
            var grid = new DataGridView {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Line,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                MultiSelect = false
            };
            grid.RowTemplate.Height = 29;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 236, 245);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
            grid.ColumnHeadersDefaultCellStyle.Font = BoldFont;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 3, 4, 3);
            grid.DefaultCellStyle.ForeColor = Ink;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(218, 232, 254);
            grid.DefaultCellStyle.SelectionForeColor = Ink;
            grid.DefaultCellStyle.Padding = new Padding(3, 0, 3, 0);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 254);
            return grid;
        }

        public static DataGridViewTextBoxColumn Column(string title, float weight, int minWidth, int maxInput,
            bool right = false)
        {
            var column = new DataGridViewTextBoxColumn {
                HeaderText = title,
                FillWeight = weight,
                MinimumWidth = minWidth,
                MaxInputLength = maxInput,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            if (right)
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            return column;
        }
    }
}
