using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PizziniMaker.Core;
using W = DocumentFormat.OpenXml.Wordprocessing;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfFontFamily = System.Windows.Media.FontFamily;

namespace PizziniMaker.App;

/// <summary>Superficie di disegno in millimetri, implementata per WPF (stampa/anteprima) e PDF.</summary>
internal interface ISlipCanvas
{
    double MeasureMm(string text, double sizePt, bool bold);

    void DrawText(string text, double xMm, double yMm, double sizePt, bool bold, bool muted = false);

    void DrawDashedRect(double xMm, double yMm, double widthMm, double heightMm);

    void DrawLine(double x1Mm, double y1Mm, double x2Mm, double y2Mm);

    void DrawPaperBox(double xMm, double yMm, double sizeMm, bool isChecked);
}

/// <summary>Foglietti A6 (quattro per A4 verticale) per stampa, PDF e Word con la stessa disposizione.</summary>
public static class SlipRenderer
{
    private const string FontName = "Segoe UI";
    private const double LineSpacing = 1.22;

    static SlipRenderer()
    {
        try
        {
            GlobalFontSettings.FontResolver ??= new WindowsFontResolver();
        }
        catch (InvalidOperationException)
        {
            // Risolutore già impostato.
        }
    }

    /// <summary>Font per il PDF letti dalla cartella dei caratteri di Windows (Segoe UI, con Arial di riserva).</summary>
    private sealed class WindowsFontResolver : IFontResolver
    {
        private static readonly string FontsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

        private static readonly (string Face, string[] Files)[] Faces =
        [
            ("Regular", ["segoeui.ttf", "arial.ttf"]),
            ("Bold", ["segoeuib.ttf", "arialbd.ttf"])
        ];

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            new(isBold ? "Bold" : "Regular");

        public byte[]? GetFont(string faceName)
        {
            var files = Faces.FirstOrDefault(face => face.Face == faceName).Files ?? Faces[0].Files;
            foreach (var file in files)
            {
                var path = System.IO.Path.Combine(FontsDirectory, file);
                if (File.Exists(path))
                {
                    return File.ReadAllBytes(path);
                }
            }
            return null;
        }
    }

    private static double LineHeightMm(double sizePt) => SlipLayout.PointsToMm(sizePt * LineSpacing);

    internal static void DrawPage(ISlipCanvas canvas, IReadOnlyList<PrintSlip> slips)
    {
        for (var index = 0; index < slips.Count && index < SlipLayout.Columns * SlipLayout.Rows; index++)
        {
            DrawSlip(canvas, slips[index], index);
        }
    }

    private static void DrawSlip(ISlipCanvas canvas, PrintSlip slip, int index)
    {
        var (originX, originY) = SlipLayout.SlotOrigin(index);
        var inset = SlipLayout.BorderInsetMm;
        var boxX = originX + inset;
        var boxY = originY + inset;
        var boxWidth = SlipLayout.SlotWidthMm - 2 * inset;
        var boxHeight = SlipLayout.SlotHeightMm - 2 * inset;
        canvas.DrawDashedRect(boxX, boxY, boxWidth, boxHeight);

        var left = boxX + SlipLayout.PaddingMm;
        var right = boxX + boxWidth - SlipLayout.PaddingMm;
        var width = right - left;
        var y = boxY + SlipLayout.PaddingMm;

        // Intestazione: dipendente in alto a sinistra, data in alto a destra.
        var dateWidth = canvas.MeasureMm(slip.DateText, SlipLayout.HeaderFontPt, true);
        canvas.DrawText(slip.DateText, right - dateWidth, y, SlipLayout.HeaderFontPt, true);
        var name = Fit(canvas, slip.EmployeeName, width - dateWidth - 4, SlipLayout.HeaderFontPt, true);
        canvas.DrawText(name, left, y, SlipLayout.HeaderFontPt, true);
        y += LineHeightMm(SlipLayout.HeaderFontPt) + 1.2;
        canvas.DrawLine(left, y, right, y);
        y += 3;

        var footerHeight = LineHeightMm(SlipLayout.FooterFontPt) + 3;
        var bottom = boxY + boxHeight - SlipLayout.PaddingMm - footerHeight;
        var lineHeight = LineHeightMm(SlipLayout.LineFontPt);
        var textLeft = left + SlipLayout.PaperBoxMm + 2.2;
        var textWidth = right - textLeft;

        foreach (var line in slip.Lines)
        {
            var wrapped = TextWrapper.Wrap(line.Text, textWidth,
                text => canvas.MeasureMm(text, SlipLayout.LineFontPt, false));
            if (y + lineHeight > bottom)
            {
                canvas.DrawText("… altre attività non stampate", textLeft, y - 1, SlipLayout.FooterFontPt, false, muted: true);
                break;
            }
            if (line.ShowPaperBox)
            {
                canvas.DrawPaperBox(left, y + (lineHeight - SlipLayout.PaperBoxMm) / 2, SlipLayout.PaperBoxMm, line.PaperChecked);
            }
            foreach (var part in wrapped)
            {
                if (y + lineHeight > bottom)
                {
                    break;
                }
                canvas.DrawText(part, textLeft, y, SlipLayout.LineFontPt, false);
                y += lineHeight;
            }
            y += 1.6;
        }

        var footerY = boxY + boxHeight - SlipLayout.PaddingMm - LineHeightMm(SlipLayout.FooterFontPt);
        canvas.DrawLine(left, footerY - 1.5, right, footerY - 1.5);
        canvas.DrawText(Fit(canvas, slip.FooterText, width, SlipLayout.FooterFontPt, true), left, footerY,
            SlipLayout.FooterFontPt, true, muted: true);
    }

    private static string Fit(ISlipCanvas canvas, string text, double widthMm, double sizePt, bool bold)
    {
        if (canvas.MeasureMm(text, sizePt, bold) <= widthMm)
        {
            return text;
        }
        var value = text;
        while (value.Length > 1 && canvas.MeasureMm(value + "…", sizePt, bold) > widthMm)
        {
            value = value[..^1];
        }
        return value.TrimEnd() + "…";
    }

    // ---------- Stampa e anteprima (WPF) ----------

    public static FixedDocument CreateFixedDocument(IReadOnlyList<IReadOnlyList<PrintSlip>> pages)
    {
        var document = new FixedDocument();
        var pageSize = new System.Windows.Size(MmToDip(SlipLayout.PageWidthMm), MmToDip(SlipLayout.PageHeightMm));
        document.DocumentPaginator.PageSize = pageSize;
        foreach (var slips in pages)
        {
            var canvas = new Canvas { Width = pageSize.Width, Height = pageSize.Height, Background = WpfBrushes.White };
            DrawPage(new WpfSlipCanvas(canvas), slips);
            var page = new FixedPage { Width = pageSize.Width, Height = pageSize.Height, Background = WpfBrushes.White };
            page.Children.Add(canvas);
            var content = new PageContent();
            ((System.Windows.Markup.IAddChild)content).AddChild(page);
            document.Pages.Add(content);
        }
        return document;
    }

    private static double MmToDip(double mm) => mm * 96 / 25.4;

    private sealed class WpfSlipCanvas(Canvas canvas) : ISlipCanvas
    {
        private static readonly Typeface Regular = new(new WpfFontFamily(FontName), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private static readonly Typeface Bold = new(new WpfFontFamily(FontName), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly SolidColorBrush Muted = new(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));
        private static readonly SolidColorBrush Ink = new(System.Windows.Media.Color.FromRgb(0x11, 0x11, 0x11));

        private static FormattedText Format(string text, double sizePt, bool bold) => new(
            text, CultureInfo.GetCultureInfo("it-IT"), System.Windows.FlowDirection.LeftToRight,
            bold ? Bold : Regular, sizePt * 96 / 72, Ink, 1.0);

        public double MeasureMm(string text, double sizePt, bool bold) =>
            Format(text, sizePt, bold).WidthIncludingTrailingWhitespace * 25.4 / 96;

        public void DrawText(string text, double xMm, double yMm, double sizePt, bool bold, bool muted = false)
        {
            var block = new TextBlock
            {
                Text = text,
                FontFamily = new WpfFontFamily(FontName),
                FontSize = sizePt * 96 / 72,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = muted ? Muted : Ink
            };
            Canvas.SetLeft(block, MmToDip(xMm));
            Canvas.SetTop(block, MmToDip(yMm));
            canvas.Children.Add(block);
        }

        public void DrawDashedRect(double xMm, double yMm, double widthMm, double heightMm)
        {
            var rectangle = new System.Windows.Shapes.Rectangle
            {
                Width = MmToDip(widthMm),
                Height = MmToDip(heightMm),
                Stroke = Muted,
                StrokeThickness = 0.9,
                StrokeDashArray = [6, 4]
            };
            Canvas.SetLeft(rectangle, MmToDip(xMm));
            Canvas.SetTop(rectangle, MmToDip(yMm));
            canvas.Children.Add(rectangle);
        }

        public void DrawLine(double x1Mm, double y1Mm, double x2Mm, double y2Mm) =>
            canvas.Children.Add(new Line
            {
                X1 = MmToDip(x1Mm), Y1 = MmToDip(y1Mm), X2 = MmToDip(x2Mm), Y2 = MmToDip(y2Mm),
                Stroke = Muted, StrokeThickness = 0.6
            });

        public void DrawPaperBox(double xMm, double yMm, double sizeMm, bool isChecked)
        {
            var box = new System.Windows.Shapes.Rectangle
            {
                Width = MmToDip(sizeMm), Height = MmToDip(sizeMm), Stroke = Ink, StrokeThickness = 1
            };
            Canvas.SetLeft(box, MmToDip(xMm));
            Canvas.SetTop(box, MmToDip(yMm));
            canvas.Children.Add(box);
            if (isChecked)
            {
                var mark = new TextBlock
                {
                    Text = "V",
                    FontFamily = new WpfFontFamily(FontName),
                    FontWeight = FontWeights.Bold,
                    FontSize = SlipLayout.LineFontPt * 0.85 * 96 / 72,
                    Foreground = Ink,
                    Width = MmToDip(sizeMm),
                    TextAlignment = TextAlignment.Center,
                    LineHeight = MmToDip(sizeMm),
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight
                };
                Canvas.SetLeft(mark, MmToDip(xMm));
                Canvas.SetTop(mark, MmToDip(yMm));
                canvas.Children.Add(mark);
            }
        }
    }

    // ---------- PDF (PDFsharp, licenza MIT) ----------

    public static void SavePdf(IReadOnlyList<IReadOnlyList<PrintSlip>> pages, string path)
    {
        using var document = new PdfDocument();
        document.Info.Title = "PizziniMaker - giorni";
        document.Info.Creator = $"PizziniMaker {AppVersion.Display}";
        foreach (var slips in pages)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromMillimeter(SlipLayout.PageWidthMm);
            page.Height = XUnit.FromMillimeter(SlipLayout.PageHeightMm);
            using var graphics = XGraphics.FromPdfPage(page);
            DrawPage(new PdfSlipCanvas(graphics), slips);
        }

        var temporary = path + ".tmp";
        document.Save(temporary);
        File.Move(temporary, path, overwrite: true);
    }

    private sealed class PdfSlipCanvas(XGraphics graphics) : ISlipCanvas
    {
        private static readonly XColor InkColor = XColor.FromArgb(0x11, 0x11, 0x11);
        private static readonly XColor MutedColor = XColor.FromArgb(0x44, 0x44, 0x44);
        private readonly Dictionary<(double, bool), XFont> _fonts = [];

        private XFont Font(double sizePt, bool bold)
        {
            if (!_fonts.TryGetValue((sizePt, bold), out var font))
            {
                font = new XFont(FontName, sizePt, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
                _fonts[(sizePt, bold)] = font;
            }
            return font;
        }

        private static double Pt(double mm) => SlipLayout.MmToPoints(mm);

        public double MeasureMm(string text, double sizePt, bool bold) =>
            SlipLayout.PointsToMm(graphics.MeasureString(text, Font(sizePt, bold)).Width);

        public void DrawText(string text, double xMm, double yMm, double sizePt, bool bold, bool muted = false) =>
            graphics.DrawString(text, Font(sizePt, bold), new XSolidBrush(muted ? MutedColor : InkColor),
                new XPoint(Pt(xMm), Pt(yMm)), XStringFormats.TopLeft);

        public void DrawDashedRect(double xMm, double yMm, double widthMm, double heightMm)
        {
            var pen = new XPen(MutedColor, 0.7) { DashStyle = XDashStyle.Custom, DashPattern = [6, 4] };
            graphics.DrawRectangle(pen, Pt(xMm), Pt(yMm), Pt(widthMm), Pt(heightMm));
        }

        public void DrawLine(double x1Mm, double y1Mm, double x2Mm, double y2Mm) =>
            graphics.DrawLine(new XPen(MutedColor, 0.45), Pt(x1Mm), Pt(y1Mm), Pt(x2Mm), Pt(y2Mm));

        public void DrawPaperBox(double xMm, double yMm, double sizeMm, bool isChecked)
        {
            graphics.DrawRectangle(new XPen(InkColor, 0.75), Pt(xMm), Pt(yMm), Pt(sizeMm), Pt(sizeMm));
            if (isChecked)
            {
                graphics.DrawString("V", Font(SlipLayout.LineFontPt * 0.85, true), new XSolidBrush(InkColor),
                    new XRect(Pt(xMm), Pt(yMm), Pt(sizeMm), Pt(sizeMm)), XStringFormats.Center);
            }
        }
    }

    // ---------- Word (Open XML SDK, licenza MIT) ----------

    private static int Twips(double mm) => (int)Math.Round(mm * 1440 / 25.4);

    public static void SaveWord(IReadOnlyList<IReadOnlyList<PrintSlip>> pages, string path)
    {
        var temporary = path + ".tmp";
        using (var document = WordprocessingDocument.Create(temporary, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            var body = new W.Body();
            main.Document = new W.Document(body);

            // Margini di 4 mm: la tabella 2 colonne × righe da 144 mm porta due righe (quattro giorni) per pagina.
            const double marginMm = 4.5;
            var cellWidthMm = (SlipLayout.PageWidthMm - 2 * marginMm) / SlipLayout.Columns;
            const double rowHeightMm = 143.5;

            var table = new W.Table(new W.TableProperties(
                new W.TableWidth { Width = Twips(cellWidthMm * 2).ToString(), Type = W.TableWidthUnitValues.Dxa },
                new W.TableBorders(
                    DashedBorder<W.TopBorder>(), DashedBorder<W.LeftBorder>(), DashedBorder<W.BottomBorder>(),
                    DashedBorder<W.RightBorder>(), DashedBorder<W.InsideHorizontalBorder>(), DashedBorder<W.InsideVerticalBorder>()),
                new W.TableLayout { Type = W.TableLayoutValues.Fixed },
                new W.TableCellMarginDefault(
                    new W.TopMargin { Width = Twips(SlipLayout.PaddingMm).ToString(), Type = W.TableWidthUnitValues.Dxa },
                    new W.TableCellLeftMargin { Width = (short)Twips(SlipLayout.PaddingMm), Type = W.TableWidthValues.Dxa },
                    new W.BottomMargin { Width = Twips(SlipLayout.PaddingMm).ToString(), Type = W.TableWidthUnitValues.Dxa },
                    new W.TableCellRightMargin { Width = (short)Twips(SlipLayout.PaddingMm), Type = W.TableWidthValues.Dxa })),
                new W.TableGrid(new W.GridColumn { Width = Twips(cellWidthMm).ToString() },
                    new W.GridColumn { Width = Twips(cellWidthMm).ToString() }));

            var allSlips = pages.SelectMany(page => page).ToArray();
            for (var index = 0; index < allSlips.Length; index += SlipLayout.Columns)
            {
                var row = new W.TableRow(new W.TableRowProperties(
                    new W.TableRowHeight { Val = (uint)Twips(rowHeightMm), HeightType = W.HeightRuleValues.Exact },
                    new W.CantSplit()));
                for (var column = 0; column < SlipLayout.Columns; column++)
                {
                    var slip = index + column < allSlips.Length ? allSlips[index + column] : null;
                    row.Append(CreateCell(slip, cellWidthMm));
                }
                table.Append(row);
            }

            body.Append(table);
            // Paragrafo finale obbligatorio, ridotto al minimo per non generare una pagina vuota.
            body.Append(new W.Paragraph(new W.ParagraphProperties(
                new W.SpacingBetweenLines { Before = "0", After = "0", Line = "20", LineRule = W.LineSpacingRuleValues.Exact }),
                new W.Run(new W.RunProperties(new W.FontSize { Val = "2" }))));
            body.Append(new W.SectionProperties(
                new W.PageSize { Width = (uint)Twips(SlipLayout.PageWidthMm), Height = (uint)Twips(SlipLayout.PageHeightMm), Orient = W.PageOrientationValues.Portrait },
                new W.PageMargin
                {
                    Top = Twips(marginMm), Bottom = Twips(marginMm), Left = (uint)Twips(marginMm), Right = (uint)Twips(marginMm),
                    Header = 0, Footer = 0, Gutter = 0
                }));
            main.Document.Save();
        }
        File.Move(temporary, path, overwrite: true);
    }

    private static T DashedBorder<T>() where T : W.BorderType, new() =>
        new() { Val = W.BorderValues.Dashed, Size = 6, Color = "444444", Space = 0 };

    private static W.TableCell CreateCell(PrintSlip? slip, double widthMm)
    {
        var cell = new W.TableCell(new W.TableCellProperties(
            new W.TableCellWidth { Width = Twips(widthMm).ToString(), Type = W.TableWidthUnitValues.Dxa }));
        if (slip is null)
        {
            cell.Append(new W.Paragraph());
            return cell;
        }

        var contentWidth = Twips(widthMm - 2 * SlipLayout.PaddingMm);
        // Intestazione: nome a sinistra, data allineata a destra con tabulazione.
        cell.Append(new W.Paragraph(
            new W.ParagraphProperties(
                new W.ParagraphBorders(new W.BottomBorder { Val = W.BorderValues.Single, Size = 4, Color = "888888", Space = 2 }),
                new W.Tabs(new W.TabStop { Val = W.TabStopValues.Right, Position = contentWidth }),
                new W.SpacingBetweenLines { After = Twips(3).ToString() }),
            TextRun(slip.EmployeeName, SlipLayout.HeaderFontPt, bold: true),
            new W.Run(new W.TabChar()),
            TextRun(slip.DateText, SlipLayout.HeaderFontPt, bold: true)));

        foreach (var line in slip.Lines)
        {
            var paragraph = new W.Paragraph(new W.ParagraphProperties(
                new W.Tabs(new W.TabStop { Val = W.TabStopValues.Left, Position = Twips(SlipLayout.PaperBoxMm + 2.2) }),
                new W.SpacingBetweenLines { After = Twips(1.6).ToString() },
                new W.Indentation { Left = Twips(SlipLayout.PaperBoxMm + 2.2).ToString(), Hanging = Twips(SlipLayout.PaperBoxMm + 2.2).ToString() }));
            if (line.ShowPaperBox)
            {
                // Quadratino: testo con bordo, con la V se l'attività è registrata su carta.
                paragraph.Append(new W.Run(
                    new W.RunProperties(
                        new W.RunFonts { Ascii = FontName, HighAnsi = FontName },
                        new W.Bold(),
                        new W.FontSize { Val = HalfPoints(SlipLayout.LineFontPt * 0.85) },
                        new W.Border { Val = W.BorderValues.Single, Size = 6, Color = "111111", Space = 0 }),
                    new W.Text(line.PaperChecked ? " V " : "    ") { Space = SpaceProcessingModeValues.Preserve }));
            }
            paragraph.Append(new W.Run(new W.TabChar()));
            paragraph.Append(TextRun(line.Text, SlipLayout.LineFontPt, bold: false));
            cell.Append(paragraph);
        }

        cell.Append(new W.Paragraph(
            new W.ParagraphProperties(
                new W.ParagraphBorders(new W.TopBorder { Val = W.BorderValues.Single, Size = 4, Color = "888888", Space = 2 }),
                new W.SpacingBetweenLines { Before = Twips(4).ToString() }),
            TextRun(slip.FooterText, SlipLayout.FooterFontPt, bold: true, color: "444444")));
        return cell;
    }

    private static string HalfPoints(double points) => ((int)Math.Round(points * 2)).ToString(CultureInfo.InvariantCulture);

    private static W.Run TextRun(string text, double sizePt, bool bold, string color = "111111")
    {
        var properties = new W.RunProperties(new W.RunFonts { Ascii = FontName, HighAnsi = FontName, ComplexScript = FontName });
        if (bold)
        {
            properties.Append(new W.Bold());
        }
        properties.Append(new W.Color { Val = color }, new W.FontSize { Val = HalfPoints(sizePt) });
        return new W.Run(properties, new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }
}
