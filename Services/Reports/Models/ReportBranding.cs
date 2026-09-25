namespace HospitalMobileAPPApi.Services.Reports.Models;

public sealed class ReportBranding
{
    public string HospitalEng { get; init; } = "BAHRIA TOWN INTERNATIONAL HOSPITAL";
    public string BranchEng { get; init; } = "KARACHI BRANCH";
    public string AddressEng { get; init; } = "Precinct 19, Phase 1, Bahria Town Karachi";
    public string PhoneNo { get; init; } = "111 111 284";
    public string HospitalUrd { get; init; } = "بحریہ ٹاؤن انٹرنیشنل ہسپتال";
    public string BranchUrd { get; init; } = "کراچی برانچ";
    public string AddressUrd { get; init; } = "پریسنٹ ۱۹ ، فیز ۱، بحریہ ٹاوُن کراچی";
    public string PrintedBy { get; init; } = "MobileApp";
    public string PrintedVia { get; init; } = "Unknown";
    public byte[]? LogoImage { get; init; }
    public byte[]? BackgroundImage { get; init; }
    public byte[]? QrCodeImage { get; init; }
    public bool ShowQrCode { get; init; }
}
