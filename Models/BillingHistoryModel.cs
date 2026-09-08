namespace HospitalMobileAPPApi.Models
{
    public class BillingHistoryModel
    {
        public string? BillId { get; set; }

        public string? MrNo { get; set; }

        public string? InvoiceNo { get; set; }

        public string? Department { get; set; }

        public DateTime? VisitDate { get; set; }

        public DateTime? PaymentDate { get; set; }

        public string? PaymentMethod { get; set; }

        public decimal Amount { get; set; }

        public string? IsCancel { get; set; }

        public string? CancelReason { get; set; }
    }


}
