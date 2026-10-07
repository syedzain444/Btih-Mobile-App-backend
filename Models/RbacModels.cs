namespace HospitalMobileAPPApi.Models
{
    public class AdminRoleRecord
    {
        public int RoleId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<string> Permissions { get; set; } = new();
    }

    public class AdminPermissionRecord
    {
        public int PermissionId { get; set; }
        public string ModuleKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
    }

    public class AdminStaffUserRecord
    {
        public int AdminId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<string> Permissions { get; set; } = new();
    }

    public class CreateAdminStaffRequest
    {
        public string Username { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string Role { get; set; } = "Staff";
        public string Password { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class UpdateAdminStaffRequest
    {
        public string? DisplayName { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class ResetAdminPasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
    }

    public class CreateAdminRoleRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public List<string>? Permissions { get; set; }
    }

    public class UpdateAdminRoleRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class SetRolePermissionsRequest
    {
        public List<string> Permissions { get; set; } = new();
    }
}
