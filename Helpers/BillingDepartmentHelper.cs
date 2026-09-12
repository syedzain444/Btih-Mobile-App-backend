namespace HospitalMobileAPPApi.Helpers
{
    public static class BillingDepartmentHelper
    {
        private static readonly Dictionary<string, string> DisplayToCode =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["EMERGENCY"] = "EMERGENCY",
                ["EMERGENCY DEPARTMENT"] = "EMERGENCY",
                ["OPD"] = "OPD",
                ["OUT PATIENT"] = "OPD",
                ["OUTPATIENT"] = "OPD",
                ["LABORATORY"] = "LABORATORY",
                ["LAB"] = "LABORATORY",
                ["SERVICES"] = "SERVICES",
                ["SERVICE"] = "SERVICES",
                ["RADIOLOGY"] = "RADIOLOGY",
                ["IPD"] = "IPD",
                ["IN PATIENT"] = "IPD",
                ["INPATIENT"] = "IPD",
                ["PROCEDURE"] = "PROCEDURE",
                ["PROCEDURES"] = "PROCEDURE",
            };

        public static string NormalizeDepartmentCode(string? departmentName)
        {
            if (string.IsNullOrWhiteSpace(departmentName))
            {
                return "UNKNOWN";
            }

            var trimmed = departmentName.Trim();
            if (DisplayToCode.TryGetValue(trimmed, out var exactCode))
            {
                return exactCode;
            }

            var upper = trimmed.ToUpperInvariant();
            foreach (var pair in DisplayToCode)
            {
                if (upper.Contains(pair.Key, StringComparison.Ordinal))
                {
                    return pair.Value;
                }
            }

            return upper.Replace(' ', '_');
        }

        public static string GetDisplayName(string departmentCode)
        {
            return departmentCode.ToUpperInvariant() switch
            {
                "EMERGENCY" => "Emergency",
                "OPD" => "OPD",
                "LABORATORY" => "Laboratory",
                "SERVICES" => "Services",
                "RADIOLOGY" => "Radiology",
                "IPD" => "IPD",
                "PROCEDURE" => "Procedure",
                _ => departmentCode,
            };
        }

        public static int GetReportId(string departmentCode)
        {
            return departmentCode.ToUpperInvariant() switch
            {
                "EMERGENCY" or "IPD" => 27,
                _ => 26,
            };
        }
    }
}
