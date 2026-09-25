using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HospitalMobileAPPApi.Services.Reports.Layout;

public static class ReportTheme
{
    public const string FontFamily = "Calibri";
    public static readonly Color Brown = Color.FromHex("#8B4513");
    public static readonly Color LightGrey = Color.FromHex("#D3D3D3");
    public static readonly Color Black = Colors.Black;

    public static TextStyle Title => TextStyle.Default.FontFamily(FontFamily).FontSize(12).Bold().FontColor(Brown);
    public static TextStyle SubTitle => TextStyle.Default.FontFamily(FontFamily).FontSize(11).Bold();
    public static TextStyle HeaderEng => TextStyle.Default.FontFamily(FontFamily).FontSize(12).Bold().FontColor(Brown);
    public static TextStyle HeaderUrd => TextStyle.Default.FontFamily(FontFamily).FontSize(12).Bold().FontColor(Brown);
    public static TextStyle Label => TextStyle.Default.FontFamily(FontFamily).FontSize(8).Bold();
    public static TextStyle Value => TextStyle.Default.FontFamily(FontFamily).FontSize(8);
    public static TextStyle TableHeader => TextStyle.Default.FontFamily(FontFamily).FontSize(11).Bold();
    public static TextStyle TableCell => TextStyle.Default.FontFamily(FontFamily).FontSize(8);
    public static TextStyle Footer => TextStyle.Default.FontFamily(FontFamily).FontSize(6);
    public static TextStyle SectionTitle => TextStyle.Default.FontFamily(FontFamily).FontSize(12).Bold();
    public static TextStyle PrescriptionLabel => TextStyle.Default.FontFamily(FontFamily).FontSize(9).Bold();
    public static TextStyle PrescriptionValue => TextStyle.Default.FontFamily(FontFamily).FontSize(9);
}
