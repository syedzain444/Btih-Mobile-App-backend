namespace HospitalMobileAPPApi.Helpers
{
    public static class AuthorizationPolicies
    {
        public const string AdminOnly = "AdminOnly";
        public const string StaffOrAdmin = "StaffOrAdmin";
        public const string PatientOnly = "PatientOnly";
    }

    public static class AppRoles
    {
        public const string Patient = "Patient";
        public const string Admin = "Admin";
        public const string Staff = "Staff";
    }
}
