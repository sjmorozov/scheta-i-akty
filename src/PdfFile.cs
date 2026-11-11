using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace InvoiceAndAct
{
    internal sealed class PdfFile
    {
        public const double Width = 595.28;
        public const double Height = 841.89;

        private readonly PdfFont regular = new PdfFont("arial.ttf", "ArialMT", "AAAAAA", false);
        private readonly PdfFont bold = new PdfFont("arialbd.ttf", "Arial-BoldMT", "AAAAAB", true);
        public readonly List<PdfPage> Pages = new List<PdfPage>();

        public PdfPage AddPage()
        {
            var page = new PdfPage(regular, bold);
            Pages.Add(page);
            return page;
        }

        public void Save(string path)
        {
            var objects = new PdfObjects();
            int catalog = objects.Reserve();
            int pageTree = objects.Reserve();
            int regularFont = regular.Write(objects);
            int boldFont = bold.Write(objects);
            var pageReferences = new List<string>();
            foreach (PdfPage page in Pages)
            {
                int content = objects.AddStream(Encoding.ASCII.GetBytes(page.Commands.ToString()), "");
                int number = objects.Add(Format(
                    "<< /Type /Page /Parent {0} 0 R /MediaBox [0 0 {1} {2}] " +
                    "/Resources << /Font << /F1 {3} 0 R /F2 {4} 0 R >> >> /Contents {5} 0 R >>",
                    pageTree, Width, Height, regularFont, boldFont, content));
                pageReferences.Add(number + " 0 R");
            }
            objects.Set(catalog, Format("<< /Type /Catalog /Pages {0} 0 R >>", pageTree));
            objects.Set(pageTree, Format("<< /Type /Pages /Count {0} /Kids [{1}] >>",
                Pages.Count, string.Join(" ", pageReferences)));

            using (var stream = new FileStream(path, FileMode.CreateNew))
            {
                objects.Save(stream, catalog);
                stream.Flush(true);
            }
        }

        internal static string Format(string value, params object[] arguments)
        {
            return string.Format(CultureInfo.InvariantCulture, value, arguments);
        }
    }

    internal enum TextAlign { Left, Center, Right }

    internal sealed class PdfPage
    {
        private readonly PdfFont regular;
        private readonly PdfFont bold;
        public readonly StringBuilder Commands = new StringBuilder();

        public PdfPage(PdfFont regular, PdfFont bold)
        {
            this.regular = regular;
            this.bold = bold;
        }

        public double Measure(string text, double size, bool heavy)
        {
            return (heavy ? bold : regular).Measure(text, size);
        }

        public IList<string> Wrap(string text, double width, double size, bool heavy)
        {
            var lines = new List<string>();
            string line = "";
            foreach (Match word in Regex.Matches(text ?? "", @"\S+"))
            {
                string combined = line.Length == 0 ? word.Value : line + " " + word.Value;
                if (Measure(combined, size, heavy) <= width)
                {
                    line = combined;
                    continue;
                }
                if (line.Length > 0)
                    lines.Add(line);
                line = "";
                if (Measure(word.Value, size, heavy) <= width)
                {
                    line = word.Value;
                    continue;
                }
                var elements = StringInfo.GetTextElementEnumerator(word.Value);
                while (elements.MoveNext())
                {
                    string element = elements.GetTextElement();
                    if (line.Length > 0 && Measure(line + element, size, heavy) > width)
                    {
                        lines.Add(line);
                        line = "";
                    }
                    line += element;
                }
            }
            if (line.Length > 0)
                lines.Add(line);
            return lines;
        }

        public void Text(string text, double x, double baseline, double size, bool heavy, TextAlign align = TextAlign.Left)
        {
            if (string.IsNullOrEmpty(text))
                return;
            PdfFont font = heavy ? bold : regular;
            if (align != TextAlign.Left)
                x -= font.Measure(text, size) / (align == TextAlign.Center ? 2 : 1);
            Commands.AppendFormat(CultureInfo.InvariantCulture,
                "BT /F{0} {1:0.##} Tf 1 0 0 1 {2:0.###} {3:0.###} Tm <{4}> Tj ET\n",
                heavy ? 2 : 1, size, x, PdfFile.Height - baseline, font.Encode(text));
        }

        public void Rule(double x1, double y1, double x2, double y2, double weight, double gray)
        {
            Commands.AppendFormat(CultureInfo.InvariantCulture,
                "q {0:0.##} G {1:0.##} w {2:0.###} {3:0.###} m {4:0.###} {5:0.###} l S Q\n",
                gray, weight, x1, PdfFile.Height - y1, x2, PdfFile.Height - y2);
        }

        public void Fill(double x, double top, double width, double height, double gray)
        {
            Commands.AppendFormat(CultureInfo.InvariantCulture,
                "q {0:0.##} g {1:0.###} {2:0.###} {3:0.###} {4:0.###} re f Q\n",
                gray, x, PdfFile.Height - top - height, width, height);
        }
    }

    internal sealed class PdfFont
    {
        private readonly GlyphTypeface face;
        private readonly string postScriptName;
        private readonly string prefix;
        private readonly bool bold;
        private readonly Dictionary<int, int> cidForCodePoint = new Dictionary<int, int>();
        private readonly List<int> codePoints = new List<int>();
        private readonly List<ushort> glyphs = new List<ushort>();

        public PdfFont(string filename, string postScriptName, string prefix, bool bold)
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            face = new GlyphTypeface(new Uri(Path.Combine(windows, "Fonts", filename)));
            this.postScriptName = postScriptName;
            this.prefix = prefix;
            this.bold = bold;
        }

        private ushort Glyph(int codePoint)
        {
            ushort glyph;
            if (!face.CharacterToGlyphMap.TryGetValue(codePoint, out glyph) || glyph == 0)
                throw new ArgumentException("Шрифт PDF не поддерживает символ «" + char.ConvertFromUtf32(codePoint) + "».");
            return glyph;
        }

        private int Advance(ushort glyph)
        {
            return (int)Math.Round(face.AdvanceWidths[glyph] * 1000);
        }

        public double Measure(string value, double size)
        {
            return CodePoints(value).Sum(codePoint => Advance(Glyph(codePoint))) * size / 1000;
        }

        public string Encode(string value)
        {
            var output = new StringBuilder();
            foreach (int codePoint in CodePoints(value))
            {
                int cid;
                if (!cidForCodePoint.TryGetValue(codePoint, out cid))
                {
                    cid = codePoints.Count + 1;
                    if (cid > ushort.MaxValue)
                        throw new ArgumentException("Слишком много разных символов для одного PDF.");
                    cidForCodePoint.Add(codePoint, cid);
                    codePoints.Add(codePoint);
                    glyphs.Add(Glyph(codePoint));
                }
                output.Append(cid.ToString("X4", CultureInfo.InvariantCulture));
            }
            return output.ToString();
        }

        private static IEnumerable<int> CodePoints(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                int codePoint = char.ConvertToUtf32(value, index);
                yield return codePoint;
                if (codePoint > char.MaxValue)
                    index++;
            }
        }

        public int Write(PdfObjects objects)
        {
            FontEmbeddingRight rights = face.EmbeddingRights;
            bool subset = rights == FontEmbeddingRight.Editable || rights == FontEmbeddingRight.Installable ||
                rights == FontEmbeddingRight.PreviewAndPrint;
            byte[] fontData;
            if (subset)
            {
                var used = new HashSet<ushort>(glyphs) { 0 };
                fontData = face.ComputeSubset(used);
            }
            else if (rights == FontEmbeddingRight.EditableButNoSubsetting ||
                rights == FontEmbeddingRight.PreviewAndPrintButNoSubsetting ||
                rights == FontEmbeddingRight.InstallableButNoSubsetting)
            {
                using (Stream source = face.GetFontStream())
                using (var output = new MemoryStream())
                {
                    source.CopyTo(output);
                    fontData = output.ToArray();
                }
            }
            else
                throw new InvalidOperationException("Системный шрифт не разрешает встраивание в PDF.");

            string name = subset ? prefix + "+" + postScriptName : postScriptName;
            int fontFile = objects.AddStream(fontData, " /Length1 " + fontData.Length);
            int ascent = (int)Math.Round(face.Baseline * 1000);
            int descent = (int)Math.Round((face.Baseline - face.Height) * 1000);
            int caps = (int)Math.Round(face.CapsHeight * 1000);
            int descriptor = objects.Add(PdfFile.Format(
                "<< /Type /FontDescriptor /FontName /{0} /Flags 32 /FontBBox [{1}] /ItalicAngle 0 " +
                "/Ascent {2} /Descent {3} /CapHeight {4} /StemV {5} /FontFile2 {6} 0 R >>",
                name, BoundingBox(fontData), ascent, descent, caps, bold ? 120 : 80, fontFile));

            byte[] glyphMap = new byte[(glyphs.Count + 1) * 2];
            for (int index = 0; index < glyphs.Count; index++)
            {
                glyphMap[(index + 1) * 2] = (byte)(glyphs[index] >> 8);
                glyphMap[(index + 1) * 2 + 1] = (byte)glyphs[index];
            }
            int cidToGid = objects.AddStream(glyphMap, "");
            string widths = string.Join(" ", glyphs.Select(glyph => Advance(glyph).ToString(CultureInfo.InvariantCulture)));
            int cidFont = objects.Add(PdfFile.Format(
                "<< /Type /Font /Subtype /CIDFontType2 /BaseFont /{0} " +
                "/CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> " +
                "/FontDescriptor {1} 0 R /CIDToGIDMap {2} 0 R /W [1 [{3}]] >>",
                name, descriptor, cidToGid, widths));
            int unicode = objects.AddStream(Encoding.ASCII.GetBytes(UnicodeMap()), "");
            return objects.Add(PdfFile.Format(
                "<< /Type /Font /Subtype /Type0 /BaseFont /{0} /Encoding /Identity-H " +
                "/DescendantFonts [{1} 0 R] /ToUnicode {2} 0 R >>", name, cidFont, unicode));
        }

        private static int ReadU16(byte[] bytes, int index)
        {
            return bytes[index] * 256 + bytes[index + 1];
        }

        private static string BoundingBox(byte[] font)
        {
            int count = ReadU16(font, 4);
            for (int index = 0; index < count; index++)
            {
                int table = 12 + index * 16;
                if (Encoding.ASCII.GetString(font, table, 4) != "head")
                    continue;
                int offset = ReadU16(font, table + 8) * 65536 + ReadU16(font, table + 10);
                int units = ReadU16(font, offset + 18);
                return string.Join(" ", Enumerable.Range(0, 4).Select(indexInBox =>
                    ((int)Math.Round(unchecked((short)ReadU16(font, offset + 36 + indexInBox * 2)) * 1000.0 / units))
                    .ToString(CultureInfo.InvariantCulture)));
            }
            throw new InvalidOperationException("Не удалось прочитать размеры системного шрифта.");
        }

        private string UnicodeMap()
        {
            var map = new StringBuilder(
                "/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n" +
                "/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n" +
                "/CMapName /InvoiceUnicode def\n/CMapType 2 def\n" +
                "1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
            for (int start = 0; start < codePoints.Count; start += 100)
            {
                int count = Math.Min(100, codePoints.Count - start);
                map.Append(count).Append(" beginbfchar\n");
                for (int index = start; index < start + count; index++)
                {
                    map.AppendFormat(CultureInfo.InvariantCulture, "<{0:X4}> <", index + 1);
                    foreach (char unit in char.ConvertFromUtf32(codePoints[index]))
                        map.AppendFormat(CultureInfo.InvariantCulture, "{0:X4}", (int)unit);
                    map.Append(">\n");
                }
                map.Append("endbfchar\n");
            }
            return map.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n").ToString();
        }
    }

    internal sealed class PdfObjects
    {
        private readonly List<byte[]> objects = new List<byte[]>();

        public int Reserve()
        {
            objects.Add(null);
            return objects.Count;
        }

        public void Set(int number, string value)
        {
            objects[number - 1] = Encoding.ASCII.GetBytes(value);
        }

        public int Add(string value)
        {
            int number = Reserve();
            Set(number, value);
            return number;
        }

        public int AddStream(byte[] data, string attributes)
        {
            byte[] encoded = Zlib(data);
            using (var stream = new MemoryStream())
            {
                Write(stream, PdfFile.Format("<< /Length {0} /Filter /FlateDecode{1} >>\nstream\n", encoded.Length, attributes));
                stream.Write(encoded, 0, encoded.Length);
                Write(stream, "\nendstream");
                objects.Add(stream.ToArray());
                return objects.Count;
            }
        }

        public void Save(Stream stream, int catalog)
        {
            Write(stream, "%PDF-1.4\n");
            byte[] marker = { 37, 226, 227, 207, 211, 10 };
            stream.Write(marker, 0, marker.Length);
            var offsets = new List<long>();
            for (int index = 0; index < objects.Count; index++)
            {
                offsets.Add(stream.Position);
                Write(stream, PdfFile.Format("{0} 0 obj\n", index + 1));
                byte[] data = objects[index];
                stream.Write(data, 0, data.Length);
                Write(stream, "\nendobj\n");
            }

            long xref = stream.Position;
            Write(stream, PdfFile.Format("xref\n0 {0}\n0000000000 65535 f \n", objects.Count + 1));
            foreach (long offset in offsets)
                Write(stream, PdfFile.Format("{0:0000000000} 00000 n \n", offset));
            Write(stream, PdfFile.Format("trailer\n<< /Size {0} /Root {1} 0 R >>\nstartxref\n{2}\n%%EOF\n",
                objects.Count + 1, catalog, xref));
        }

        private static void Write(Stream stream, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static byte[] Zlib(byte[] bytes)
        {
            // PDF FlateDecode expects a zlib header and Adler-32 around raw Deflate.
            uint a = 1;
            uint b = 0;
            foreach (byte value in bytes)
            {
                a = (a + value) % 65521;
                b = (b + a) % 65521;
            }
            using (var stream = new MemoryStream())
            {
                stream.WriteByte(0x78);
                stream.WriteByte(0x9c);
                using (var compressor = new DeflateStream(stream, CompressionMode.Compress, true))
                    compressor.Write(bytes, 0, bytes.Length);
                stream.WriteByte((byte)(b >> 8));
                stream.WriteByte((byte)b);
                stream.WriteByte((byte)(a >> 8));
                stream.WriteByte((byte)a);
                return stream.ToArray();
            }
        }
    }
}
