using HospitalMobileAPPApi.Services.Reports.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace HospitalMobileAPPApi.Services.Reports.Layout;

public static class ReportLayoutComponents
{
    public static void ApplyPageBackground(this PageDescriptor page, ReportBranding branding)
    {
        if (branding.BackgroundImage is { Length: > 0 })
        {
            page.Background()
                .AlignCenter()
                .AlignMiddle()
                .Image(branding.BackgroundImage)
                .FitArea();
        }
    }

    public static void ComposeHospitalHeader(this IContainer container, ReportBranding branding)
    {
        container.Row(row =>
        {
            row.ConstantItem(65).Element(c =>
            {
                if (branding.LogoImage is { Length: > 0 })
                    c.Image(branding.LogoImage).FitArea();
            });

            row.RelativeItem().Column(col =>
            {
                col.Item().AlignCenter().Text(branding.HospitalEng).Style(ReportTheme.HeaderEng);
                col.Item().AlignCenter().Text(branding.BranchEng).Style(ReportTheme.HeaderEng);
                col.Item().AlignCenter().Text(branding.AddressEng).Style(ReportTheme.Value.FontSize(9));
                col.Item().AlignCenter().Text(branding.PhoneNo).Style(ReportTheme.Value.FontSize(9));
            });

            row.RelativeItem().Column(col =>
            {
                col.Item().AlignRight().Text(branding.HospitalUrd).Style(ReportTheme.HeaderUrd);
                col.Item().AlignRight().Text(branding.BranchUrd).Style(ReportTheme.HeaderUrd);
                col.Item().AlignRight().Text(branding.AddressUrd).Style(ReportTheme.Value.FontSize(9));
                col.Item().AlignRight().Text(branding.PhoneNo).Style(ReportTheme.Value.FontSize(9));
            });
        });
    }

    public static void ComposeCenteredTitle(this IContainer container, string title)
    {
        container
            .BorderTop(0.5f).BorderBottom(0.5f).BorderColor(ReportTheme.LightGrey)
            .PaddingVertical(4)
            .AlignCenter()
            .Text(title)
            .Style(ReportTheme.SubTitle);
    }

    public static void ComposeLabelValueRow(
        this IContainer container,
        params (string Label, string Value)[] pairs)
    {
        container.Row(row =>
        {
            foreach (var (label, value) in pairs)
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(label).Style(ReportTheme.Label);
                    col.Item().Text($":   {value}").Style(ReportTheme.Value);
                });
            }
        });
    }

    public static void ComposeSignatureBlock(
        this IContainer container,
        string? preparedBy,
        string? preparedType,
        string? preparedOn,
        string? verifiedBy,
        string? verifiedType,
        string? qualifications,
        string? verifiedOn)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Prepared By").Style(ReportTheme.Label.FontSize(9));
                col.Item().Text(preparedBy ?? string.Empty).Style(ReportTheme.Value.FontSize(9));
                col.Item().Text(preparedType ?? string.Empty).Style(ReportTheme.Value.FontSize(8));
                col.Item().Text(preparedOn ?? string.Empty).Style(ReportTheme.Value.FontSize(8));
            });

            row.RelativeItem().Column(col =>
            {
                col.Item().AlignRight().Text("Verified By").Style(ReportTheme.Label.FontSize(9));
                col.Item().AlignRight().Text(verifiedBy ?? string.Empty).Style(ReportTheme.Value.FontSize(9));
                col.Item().AlignRight().Text(qualifications ?? string.Empty).Style(ReportTheme.Value.FontSize(8));
                col.Item().AlignRight().Text(verifiedType ?? string.Empty).Style(ReportTheme.Value.FontSize(8));
                col.Item().AlignRight().Text(verifiedOn ?? string.Empty).Style(ReportTheme.Value.FontSize(8));
            });
        });
    }

    public static void ComposePrintAuditFooter(this IContainer container, ReportBranding branding)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("User ID#").Style(ReportTheme.Footer.Bold());
                col.Item().Text("Printed By").Style(ReportTheme.Footer.Bold());
                col.Item().Text("Printed Via").Style(ReportTheme.Footer.Bold());
                col.Item().Text("Printed On").Style(ReportTheme.Footer.Bold());
            });

            row.RelativeItem().Column(col =>
            {
                col.Item().Text(branding.PrintedBy).Style(ReportTheme.Footer);
                col.Item().Text(branding.PrintedBy).Style(ReportTheme.Footer);
                col.Item().Text(branding.PrintedVia).Style(ReportTheme.Footer);
                col.Item().Text(DateTime.Now.ToString("g")).Style(ReportTheme.Footer);
            });

            if (branding.ShowQrCode && branding.QrCodeImage is { Length: > 0 })
            {
                row.ConstantItem(70).AlignRight().Image(branding.QrCodeImage).FitArea();
            }
        });
    }

    public static void ComposeMedicoLegalDisclaimer(this IContainer container)
    {
        container
            .PaddingTop(8)
            .AlignCenter()
            .Text("THESE REPORTS ARE NOT VALID FOR MEDICOLEGAL PURPOSES")
            .Style(ReportTheme.Label.FontSize(9).Italic());
    }

    public static void ComposeSectionHeading(this IContainer container, string heading)
    {
        container
            .PaddingTop(6)
            .PaddingBottom(2)
            .Text(heading)
            .Style(ReportTheme.SectionTitle);
    }

    public static void ComposeTextSection(this IContainer container, string heading, string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return;

        container.Column(col =>
        {
            col.Item().Text(heading).Style(ReportTheme.Label.FontSize(10));
            col.Item().PaddingTop(2).Text(body).Style(ReportTheme.Value.FontSize(10));
            col.Item().PaddingBottom(4);
        });
    }
}
