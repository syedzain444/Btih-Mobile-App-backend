namespace HospitalMobileAPPApi.Helpers
{
    /// <summary>
    /// Admin-panel RBAC (REQ-2026-025 / TC-025).
    /// Roles: Admin (full), Staff (operations), Reception (front desk).
    /// </summary>
    public static class AuthorizationPolicies
    {
        public const string AdminOnly = "AdminOnly";
        /// <summary>Admin or Staff — operational modules (refills, tickets, promotions).</summary>
        public const string StaffOrAdmin = "StaffOrAdmin";
        /// <summary>Any admin-panel role including Reception.</summary>
        public const string PortalAccess = "PortalAccess";
        public const string PatientOnly = "PatientOnly";
    }

    public static class AppRoles
    {
        public const string Patient = "Patient";
        public const string Admin = "Admin";
        public const string Staff = "Staff";
        public const string Reception = "Reception";

        public static readonly string[] PortalRoles =
        [
            Admin,
            Staff,
            Reception,
        ];

        public static bool IsPortalRole(string? role) =>
            !string.IsNullOrWhiteSpace(role) &&
            PortalRoles.Any(r => string.Equals(r, role.Trim(), StringComparison.OrdinalIgnoreCase));

        public static string NormalizePortalRole(string? role)
        {
            if (string.IsNullOrWhiteSpace(role)) return Admin;
            foreach (var known in PortalRoles)
            {
                if (string.Equals(known, role.Trim(), StringComparison.OrdinalIgnoreCase))
                    return known;
            }

            return Admin;
        }
    }

    /// <summary>Module keys shared with the admin panel permission matrix.</summary>
    public static class AdminModules
    {
        public const string Dashboard = "Dashboard";
        public const string Messages = "Messages";
        public const string Appointments = "Appointments";
        public const string Users = "Users";
        public const string Reports = "Reports";
        public const string Analytics = "Analytics";
        public const string Refills = "Refills";
        public const string Tickets = "Tickets";
        public const string Promotions = "Promotions";
        public const string Offers = "Offers";
        public const string SupportContent = "SupportContent";
        public const string Audit = "Audit";
        public const string AccessControl = "AccessControl";

        public static readonly (string Key, string Name, string Description, int Sort)[] Catalog =
        [
            (Dashboard, "Dashboard", "Overview and operational summary", 10),
            (Messages, "Messages", "Patient messaging inbox", 20),
            (Appointments, "Appointments", "Appointment queue and approvals", 30),
            (Users, "Patients", "Patient directory", 40),
            (Refills, "Refills", "Medication refill requests", 50),
            (Tickets, "Support tickets", "Complaints and support tickets", 60),
            (Promotions, "Promotions", "App splash promotions", 70),
            (Offers, "Offers & Packages", "Health packages shown in the app", 80),
            (SupportContent, "Help content", "FAQs and support contact details", 90),
            (Reports, "Reports & audit", "Operational reports and audit log", 100),
            (Analytics, "Analytics", "Usage analytics widgets", 110),
            (Audit, "Audit log", "Security and change audit trail", 120),
            (AccessControl, "Access control", "Manage admin users, roles, and permissions", 130),
        ];
    }
}
