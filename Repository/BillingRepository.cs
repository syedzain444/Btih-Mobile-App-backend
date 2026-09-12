using System.Data;
using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public class BillingRepository : IBillingRepository
    {
        private readonly IConfiguration _configuration;

        public BillingRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IReadOnlyList<BillingInvoiceDto>> GetInvoicesByMrNoAsync(string mrNo)
        {
            var result = new List<BillingInvoiceDto>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var connection = new OracleConnection(connStr);
            await using var cmd = new OracleCommand("SP_BILL_PAY", connection)
            {
                CommandType = CommandType.StoredProcedure,
            };
            cmd.BindByName = true;

            cmd.Parameters.Add("COND", OracleDbType.Int32).Value = 40;
            cmd.Parameters.Add("VAR_PAY_ID", OracleDbType.Int32).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_VISIT_ID", OracleDbType.Int32).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_PAID_AMOUNT", OracleDbType.Decimal).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_REFUND_AMOUNT", OracleDbType.Decimal).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_BALANCE_AMOUNT", OracleDbType.Decimal).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_TOTAL_AMOUNT", OracleDbType.Decimal).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_ADVANCE_AMOUNT", OracleDbType.Decimal).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_PAYMENT_METHOD", OracleDbType.Int32).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_PAYMETHOD_CODE", OracleDbType.Varchar2, 50).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_DT_PAYMENT", OracleDbType.Date).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_RECEIVED_BY", OracleDbType.Int32).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_INVOICE_NO", OracleDbType.Varchar2, 50).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_IS_CANCEL", OracleDbType.Varchar2, 10).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_DT_CANCEL", OracleDbType.Date).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_CANCEL_BY", OracleDbType.Int32).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_CANCEL_REASON", OracleDbType.Varchar2, 200).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_ARREARS_AMOUNT", OracleDbType.Decimal).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_INSURANCE_PANEL_ID", OracleDbType.Int32).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_IS_REFUND", OracleDbType.Varchar2, 10).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_MR_NO", OracleDbType.Varchar2, 50).Value = mrNo;
            cmd.Parameters.Add("VAR_REMARKS", OracleDbType.Varchar2, 200).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_DISCOUNT_AMOUNT", OracleDbType.Decimal).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_AFTER_DISCOUNT_AMOUNT", OracleDbType.Decimal).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_PNL_AVAILABLE", OracleDbType.Decimal).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_PNL_AMOUNT", OracleDbType.Decimal).Value = DBNull.Value;
            cmd.Parameters.Add("VAR_PNL_BALANCE", OracleDbType.Decimal).Value = DBNull.Value;

            cmd.Parameters.Add("PI_CURSOR", OracleDbType.RefCursor)
                .Direction = ParameterDirection.Output;

            await connection.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(MapInvoice(reader, mrNo));
            }

            return result;
        }

        private static BillingInvoiceDto MapInvoice(OracleDataReader reader, string mrNo)
        {
            var departmentName = GetString(reader, "BILL_DPT_NAME", "DEPARTMENT", "DPT_NAME");
            var departmentCode = BillingDepartmentHelper.NormalizeDepartmentCode(departmentName);
            var isCancel = GetString(reader, "IS_CANCEL", "VAR_IS_CANCEL");
            var paymentDate = GetDateTime(reader, "DT_PAYMENT", "PAYMENT_DATE");
            var amount = GetDecimal(reader, "TOTAL_AMOUNT", "AMOUNT") ?? 0m;
            var paidAmount = GetDecimal(reader, "PAID_AMOUNT", "VAR_PAID_AMOUNT");
            var balanceAmount = GetDecimal(reader, "BALANCE_AMOUNT", "VAR_BALANCE_AMOUNT", "ARREARS_AMOUNT");

            return new BillingInvoiceDto
            {
                BillId = GetString(reader, "BILLID", "BILL_ID"),
                MrNo = GetString(reader, "MR_NO", "MRNO") ?? mrNo,
                InvoiceNo = GetString(reader, "INVOICENO", "INVOICE_NO"),
                Department = departmentName ?? BillingDepartmentHelper.GetDisplayName(departmentCode),
                DepartmentCode = departmentCode,
                VisitDate = GetDateTime(reader, "VISIT_DATE", "DT_VISIT", "VISIT_DT"),
                PaymentDate = paymentDate,
                PaymentMethod = GetString(reader, "PAYMETHOD_CODE", "PAYMENT_METHOD", "PAY_METHOD"),
                Amount = amount,
                PaidAmount = paidAmount,
                BalanceAmount = balanceAmount,
                IsCancel = isCancel,
                CancelReason = GetString(reader, "CANCEL_REASON", "VAR_CANCEL_REASON"),
                PaymentStatus = ResolvePaymentStatus(isCancel, paymentDate, balanceAmount, amount),
                ReportId = BillingDepartmentHelper.GetReportId(departmentCode),
            };
        }

        private static string ResolvePaymentStatus(
            string? isCancel,
            DateTime? paymentDate,
            decimal? balanceAmount,
            decimal amount)
        {
            if (IsTruthy(isCancel))
            {
                return "cancelled";
            }

            if (balanceAmount.HasValue && balanceAmount.Value > 0)
            {
                return "pending";
            }

            if (paymentDate.HasValue)
            {
                return "paid";
            }

            if (amount <= 0)
            {
                return "paid";
            }

            return "pending";
        }

        private static bool IsTruthy(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim().ToUpperInvariant();
            return normalized is "Y" or "YES" or "1" or "TRUE";
        }

        private static string? GetString(OracleDataReader reader, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                try
                {
                    var ordinal = reader.GetOrdinal(columnName);
                    if (reader.IsDBNull(ordinal))
                    {
                        continue;
                    }

                    return reader.GetValue(ordinal)?.ToString();
                }
                catch (IndexOutOfRangeException)
                {
                    // Column not present in this cursor shape.
                }
            }

            return null;
        }

        private static DateTime? GetDateTime(OracleDataReader reader, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                try
                {
                    var ordinal = reader.GetOrdinal(columnName);
                    if (reader.IsDBNull(ordinal))
                    {
                        continue;
                    }

                    return Convert.ToDateTime(reader.GetValue(ordinal));
                }
                catch (IndexOutOfRangeException)
                {
                    // Column not present in this cursor shape.
                }
            }

            return null;
        }

        private static decimal? GetDecimal(OracleDataReader reader, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                try
                {
                    var ordinal = reader.GetOrdinal(columnName);
                    if (reader.IsDBNull(ordinal))
                    {
                        continue;
                    }

                    return Convert.ToDecimal(reader.GetValue(ordinal));
                }
                catch (IndexOutOfRangeException)
                {
                    // Column not present in this cursor shape.
                }
            }

            return null;
        }
    }
}
