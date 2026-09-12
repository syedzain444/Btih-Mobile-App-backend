using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public class BillingService : IBillingService
    {
        private readonly IBillingRepository _repository;

        public BillingService(IBillingRepository repository)
        {
            _repository = repository;
        }

        public async Task<BillingOverviewDto> GetOverviewAsync(string mrNo)
        {
            var invoices = await _repository.GetInvoicesByMrNoAsync(mrNo.Trim());
            var activeInvoices = invoices.Where(i => !IsCancelled(i)).ToList();
            var paidInvoices = activeInvoices.Where(i => i.PaymentStatus == "paid").ToList();
            var pendingInvoices = activeInvoices.Where(i => i.PaymentStatus == "pending").ToList();

            var departments = activeInvoices
                .GroupBy(i => i.DepartmentCode ?? BillingDepartmentHelper.NormalizeDepartmentCode(i.Department))
                .Select(group =>
                {
                    var code = group.Key;
                    return new BillingDepartmentSummaryDto
                    {
                        DepartmentCode = code,
                        DepartmentName = BillingDepartmentHelper.GetDisplayName(code),
                        BillCount = group.Count(),
                        TotalAmount = group.Sum(i => i.Amount),
                        ReportId = BillingDepartmentHelper.GetReportId(code),
                    };
                })
                .OrderBy(d => d.DepartmentName)
                .ToList();

            return new BillingOverviewDto
            {
                MrNo = mrNo.Trim(),
                TotalBillCount = activeInvoices.Count,
                TotalAmount = activeInvoices.Sum(i => i.Amount),
                PaidBillCount = paidInvoices.Count,
                PaidAmount = paidInvoices.Sum(i => i.Amount),
                PendingBillCount = pendingInvoices.Count,
                PendingAmount = pendingInvoices.Sum(i => ResolvePendingAmount(i)),
                CancelledBillCount = invoices.Count(IsCancelled),
                Departments = departments,
            };
        }

        public async Task<BillingHistoryResultDto> GetHistoryAsync(
            string mrNo,
            string? departmentCode = null,
            int? year = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string? search = null,
            string? paymentStatus = null)
        {
            var invoices = await _repository.GetInvoicesByMrNoAsync(mrNo.Trim());
            IEnumerable<BillingInvoiceDto> filtered = invoices;

            if (!string.IsNullOrWhiteSpace(departmentCode))
            {
                var normalizedDepartment = departmentCode.Trim().ToUpperInvariant();
                filtered = filtered.Where(i =>
                    string.Equals(i.DepartmentCode, normalizedDepartment, StringComparison.OrdinalIgnoreCase));
            }

            if (year.HasValue)
            {
                filtered = filtered.Where(i => GetReferenceDate(i)?.Year == year.Value);
            }

            if (dateFrom.HasValue)
            {
                var from = dateFrom.Value.Date;
                filtered = filtered.Where(i =>
                {
                    var date = GetReferenceDate(i);
                    return date.HasValue && date.Value.Date >= from;
                });
            }

            if (dateTo.HasValue)
            {
                var to = dateTo.Value.Date;
                filtered = filtered.Where(i =>
                {
                    var date = GetReferenceDate(i);
                    return date.HasValue && date.Value.Date <= to;
                });
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var query = search.Trim();
                filtered = filtered.Where(i =>
                    Contains(i.BillId, query)
                    || Contains(i.InvoiceNo, query)
                    || Contains(i.Department, query)
                    || Contains(i.Amount.ToString("0.##"), query));
            }

            if (!string.IsNullOrWhiteSpace(paymentStatus))
            {
                var status = paymentStatus.Trim().ToLowerInvariant();
                filtered = filtered.Where(i =>
                    string.Equals(i.PaymentStatus, status, StringComparison.OrdinalIgnoreCase));
            }

            var items = filtered
                .OrderByDescending(i => GetReferenceDate(i) ?? DateTime.MinValue)
                .ThenByDescending(i => i.BillId)
                .ToList();

            return new BillingHistoryResultDto
            {
                MrNo = mrNo.Trim(),
                TotalCount = items.Count,
                TotalAmount = items.Sum(i => i.Amount),
                Items = items,
            };
        }

        public async Task<BillingInvoiceDto?> GetInvoiceAsync(string mrNo, string billId)
        {
            if (string.IsNullOrWhiteSpace(billId))
            {
                return null;
            }

            var invoices = await _repository.GetInvoicesByMrNoAsync(mrNo.Trim());
            return invoices.FirstOrDefault(i =>
                string.Equals(i.BillId, billId.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public async Task<BillingPaymentSummaryDto> GetPaymentSummaryAsync(string mrNo)
        {
            var invoices = await _repository.GetInvoicesByMrNoAsync(mrNo.Trim());
            var activeInvoices = invoices.Where(i => !IsCancelled(i)).ToList();
            var paidInvoices = activeInvoices
                .Where(i => i.PaymentStatus == "paid")
                .OrderByDescending(i => i.PaymentDate ?? DateTime.MinValue)
                .ToList();
            var pendingInvoices = activeInvoices
                .Where(i => i.PaymentStatus == "pending")
                .OrderByDescending(i => GetReferenceDate(i) ?? DateTime.MinValue)
                .ToList();

            return new BillingPaymentSummaryDto
            {
                MrNo = mrNo.Trim(),
                TotalPaidAmount = paidInvoices.Sum(i => i.Amount),
                TotalPendingAmount = pendingInvoices.Sum(ResolvePendingAmount),
                PaidBillCount = paidInvoices.Count,
                PendingBillCount = pendingInvoices.Count,
                PendingBills = pendingInvoices,
                RecentPayments = paidInvoices.Take(10).ToList(),
            };
        }

        private static bool IsCancelled(BillingInvoiceDto invoice)
        {
            return string.Equals(invoice.PaymentStatus, "cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(invoice.IsCancel, "Y", StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime? GetReferenceDate(BillingInvoiceDto invoice)
        {
            return invoice.PaymentDate ?? invoice.VisitDate;
        }

        private static decimal ResolvePendingAmount(BillingInvoiceDto invoice)
        {
            if (invoice.BalanceAmount.HasValue && invoice.BalanceAmount.Value > 0)
            {
                return invoice.BalanceAmount.Value;
            }

            return invoice.Amount;
        }

        private static bool Contains(string? value, string query)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }
}
