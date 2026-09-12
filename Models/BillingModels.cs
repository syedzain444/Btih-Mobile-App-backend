namespace HospitalMobileAPPApi.Models
{
    public class BillingInvoiceDto
    {
        public string? BillId { get; set; }

        public string? MrNo { get; set; }

        public string? InvoiceNo { get; set; }

        public string? Department { get; set; }

        public string? DepartmentCode { get; set; }

        public DateTime? VisitDate { get; set; }

        public DateTime? PaymentDate { get; set; }

        public string? PaymentMethod { get; set; }

        public decimal Amount { get; set; }

        public decimal? PaidAmount { get; set; }

        public decimal? BalanceAmount { get; set; }

        public string? IsCancel { get; set; }

        public string? CancelReason { get; set; }

        public string PaymentStatus { get; set; } = "unknown";

        public int ReportId { get; set; }
    }

    public class BillingDepartmentSummaryDto
    {
        public string DepartmentCode { get; set; } = string.Empty;

        public string DepartmentName { get; set; } = string.Empty;

        public int BillCount { get; set; }

        public decimal TotalAmount { get; set; }

        public int ReportId { get; set; }
    }

    public class BillingOverviewDto
    {
        public string MrNo { get; set; } = string.Empty;

        public int TotalBillCount { get; set; }

        public decimal TotalAmount { get; set; }

        public int PaidBillCount { get; set; }

        public decimal PaidAmount { get; set; }

        public int PendingBillCount { get; set; }

        public decimal PendingAmount { get; set; }

        public int CancelledBillCount { get; set; }

        public IReadOnlyList<BillingDepartmentSummaryDto> Departments { get; set; } =
            Array.Empty<BillingDepartmentSummaryDto>();
    }

    public class BillingHistoryResultDto
    {
        public string MrNo { get; set; } = string.Empty;

        public int TotalCount { get; set; }

        public decimal TotalAmount { get; set; }

        public IReadOnlyList<BillingInvoiceDto> Items { get; set; } =
            Array.Empty<BillingInvoiceDto>();
    }

    public class BillingPaymentSummaryDto
    {
        public string MrNo { get; set; } = string.Empty;

        public decimal TotalPaidAmount { get; set; }

        public decimal TotalPendingAmount { get; set; }

        public int PaidBillCount { get; set; }

        public int PendingBillCount { get; set; }

        public IReadOnlyList<BillingInvoiceDto> PendingBills { get; set; } =
            Array.Empty<BillingInvoiceDto>();

        public IReadOnlyList<BillingInvoiceDto> RecentPayments { get; set; } =
            Array.Empty<BillingInvoiceDto>();
    }
}
