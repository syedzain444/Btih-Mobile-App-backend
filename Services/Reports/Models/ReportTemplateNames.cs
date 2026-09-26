namespace HospitalMobileAPPApi.Services.Reports.Models;

public static class ReportTemplateNames
{
    public const string Lab = "Labrpt";
    public const string Gastro = "GastRpt";
    public const string Radiology = "RadRpt";
    public const string Prescription = "PRESCRIPTION_A4";
    public const string BillingReceipt = "Advance_Reciept";
    public const string DischargeCertificate = "DischargeCerificate";

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var stem = Path.GetFileNameWithoutExtension(value.Trim());
        return stem switch
        {
            _ when stem.Equals(Lab, StringComparison.OrdinalIgnoreCase) => Lab,
            _ when stem.Equals("Laboratory", StringComparison.OrdinalIgnoreCase) => Lab,
            _ when stem.Equals("Lab", StringComparison.OrdinalIgnoreCase) => Lab,
            _ when stem.Equals(Gastro, StringComparison.OrdinalIgnoreCase) => Gastro,
            _ when stem.Equals("Gastroenterology", StringComparison.OrdinalIgnoreCase) => Gastro,
            _ when stem.Equals(Radiology, StringComparison.OrdinalIgnoreCase) => Radiology,
            _ when stem.Equals("Rad", StringComparison.OrdinalIgnoreCase) => Radiology,
            _ when stem.Equals(Prescription, StringComparison.OrdinalIgnoreCase) => Prescription,
            _ when stem.Equals(BillingReceipt, StringComparison.OrdinalIgnoreCase) => BillingReceipt,
            _ when stem.Equals("Advance_Receipt", StringComparison.OrdinalIgnoreCase) => BillingReceipt,
            _ when stem.Equals("Billing", StringComparison.OrdinalIgnoreCase) => BillingReceipt,
            _ when stem.Equals("Receipt", StringComparison.OrdinalIgnoreCase) => BillingReceipt,
            _ when stem.Equals(DischargeCertificate, StringComparison.OrdinalIgnoreCase) => DischargeCertificate,
            _ when stem.Equals("DischargeCertificate", StringComparison.OrdinalIgnoreCase) => DischargeCertificate,
            _ when stem.Equals("Discharge", StringComparison.OrdinalIgnoreCase) => DischargeCertificate,
            _ => stem
        };
    }

    public static bool IsSupported(string? value)
    {
        var normalized = Normalize(value);
        return normalized is Lab or Gastro or Radiology or Prescription or BillingReceipt or DischargeCertificate;
    }
}
