using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Repository
{
    public interface IBillingRepository
    {
        Task<IReadOnlyList<BillingInvoiceDto>> GetInvoicesByMrNoAsync(string mrNo);
    }
}
