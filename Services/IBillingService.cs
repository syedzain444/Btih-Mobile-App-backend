using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Services
{
    public interface IBillingService
    {
        Task<BillingOverviewDto> GetOverviewAsync(string mrNo);

        Task<BillingHistoryResultDto> GetHistoryAsync(
            string mrNo,
            string? departmentCode = null,
            int? year = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string? search = null,
            string? paymentStatus = null);

        Task<BillingInvoiceDto?> GetInvoiceAsync(string mrNo, string billId);

        Task<BillingPaymentSummaryDto> GetPaymentSummaryAsync(string mrNo);
    }
}
