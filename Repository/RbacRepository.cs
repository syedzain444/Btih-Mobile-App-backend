using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public interface IRbacRepository
    {
        Task<List<AdminPermissionRecord>> GetPermissionsAsync();
        Task<List<AdminRoleRecord>> GetRolesAsync(bool includeInactive = true);
        Task<AdminRoleRecord?> GetRoleByIdAsync(int roleId);
        Task<AdminRoleRecord?> GetRoleByCodeAsync(string code);
        Task<AdminRoleRecord> InsertRoleAsync(AdminRoleRecord role);
        Task<bool> UpdateRoleAsync(AdminRoleRecord role);
        Task<bool> DeleteRoleAsync(int roleId);
        Task SetRolePermissionsAsync(int roleId, IReadOnlyList<string> moduleKeys);
        Task<List<string>> GetPermissionKeysForRoleAsync(string roleCode);
        Task<List<AdminStaffUserRecord>> GetStaffUsersAsync();
        Task<AdminStaffUserRecord?> GetStaffUserByIdAsync(int adminId);
        Task<bool> UsernameExistsAsync(string username, int? excludeAdminId = null);
        Task<AdminStaffUserRecord> InsertStaffUserAsync(AdminStaffUserRecord user, string passwordHash);
        Task<bool> UpdateStaffUserAsync(AdminStaffUserRecord user);
        Task<bool> UpdateStaffPasswordAsync(int adminId, string passwordHash);
        Task<int> CountActiveUsersWithRoleAsync(string roleCode);
        Task EnsureAdminUserTableAsync();
    }

    public class RbacRepository : IRbacRepository
    {
        private readonly IConfiguration _configuration;

        public RbacRepository(IConfiguration configuration) => _configuration = configuration;

        public async Task<List<AdminPermissionRecord>> GetPermissionsAsync()
        {
            var result = new List<AdminPermissionRecord>();
            await using var conn = Open();
            await using var cmd = new OracleCommand(@"
                SELECT PERMISSION_ID, MODULE_KEY, NAME, DESCRIPTION, SORT_ORDER
                FROM MOBILE_ADMIN_PERMISSION
                ORDER BY SORT_ORDER ASC, PERMISSION_ID ASC", conn);
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new AdminPermissionRecord
                {
                    PermissionId = Convert.ToInt32(reader["PERMISSION_ID"]),
                    ModuleKey = reader["MODULE_KEY"]?.ToString() ?? string.Empty,
                    Name = reader["NAME"]?.ToString() ?? string.Empty,
                    Description = reader["DESCRIPTION"] == DBNull.Value ? null : reader["DESCRIPTION"]?.ToString(),
                    SortOrder = Convert.ToInt32(reader["SORT_ORDER"]),
                });
            }

            return result;
        }

        public async Task<List<AdminRoleRecord>> GetRolesAsync(bool includeInactive = true)
        {
            var roles = new List<AdminRoleRecord>();
            await using var conn = Open();
            await using var cmd = new OracleCommand(@"
                SELECT ROLE_ID, CODE, NAME, DESCRIPTION, IS_SYSTEM, IS_ACTIVE, CREATED_AT, UPDATED_AT
                FROM MOBILE_ADMIN_ROLE
                WHERE (:include_inactive = 1 OR IS_ACTIVE = 'Y')
                ORDER BY IS_SYSTEM DESC, NAME ASC", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("include_inactive", OracleDbType.Int32).Value = includeInactive ? 1 : 0;
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                roles.Add(MapRole(reader));
            }

            foreach (var role in roles)
            {
                role.Permissions = await GetPermissionKeysForRoleIdAsync(conn, role.RoleId);
            }

            return roles;
        }

        public async Task<AdminRoleRecord?> GetRoleByIdAsync(int roleId)
        {
            await using var conn = Open();
            await using var cmd = new OracleCommand(@"
                SELECT ROLE_ID, CODE, NAME, DESCRIPTION, IS_SYSTEM, IS_ACTIVE, CREATED_AT, UPDATED_AT
                FROM MOBILE_ADMIN_ROLE WHERE ROLE_ID = :id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = roleId;
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            var role = MapRole(reader);
            role.Permissions = await GetPermissionKeysForRoleIdAsync(conn, role.RoleId);
            return role;
        }

        public async Task<AdminRoleRecord?> GetRoleByCodeAsync(string code)
        {
            await using var conn = Open();
            await using var cmd = new OracleCommand(@"
                SELECT ROLE_ID, CODE, NAME, DESCRIPTION, IS_SYSTEM, IS_ACTIVE, CREATED_AT, UPDATED_AT
                FROM MOBILE_ADMIN_ROLE WHERE UPPER(CODE) = UPPER(:code)", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("code", OracleDbType.Varchar2).Value = code.Trim();
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            var role = MapRole(reader);
            role.Permissions = await GetPermissionKeysForRoleIdAsync(conn, role.RoleId);
            return role;
        }

        public async Task<AdminRoleRecord> InsertRoleAsync(AdminRoleRecord role)
        {
            await using var conn = Open();
            await conn.OpenAsync();
            int id;
            await using (var seq = new OracleCommand("SELECT MOBILE_ADMIN_ROLE_SEQ.NEXTVAL FROM DUAL", conn))
            {
                id = Convert.ToInt32((await seq.ExecuteScalarAsync())!.ToString());
            }

            await using var cmd = new OracleCommand(@"
                INSERT INTO MOBILE_ADMIN_ROLE (
                    ROLE_ID, CODE, NAME, DESCRIPTION, IS_SYSTEM, IS_ACTIVE, CREATED_AT, UPDATED_AT
                ) VALUES (
                    :id, :code, :name, :description, 'N', :is_active, SYSDATE, SYSDATE
                )", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = id;
            cmd.Parameters.Add("code", OracleDbType.Varchar2).Value = Truncate(role.Code.Trim(), 40);
            cmd.Parameters.Add("name", OracleDbType.Varchar2).Value = Truncate(role.Name.Trim(), 80);
            cmd.Parameters.Add("description", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(role.Description) ? DBNull.Value : Truncate(role.Description.Trim(), 400);
            cmd.Parameters.Add("is_active", OracleDbType.Char).Value = role.IsActive ? "Y" : "N";
            await cmd.ExecuteNonQueryAsync();
            return (await GetRoleByIdAsync(id))!;
        }

        public async Task<bool> UpdateRoleAsync(AdminRoleRecord role)
        {
            await using var conn = Open();
            await using var cmd = new OracleCommand(@"
                UPDATE MOBILE_ADMIN_ROLE
                SET NAME = :name,
                    DESCRIPTION = :description,
                    IS_ACTIVE = :is_active,
                    UPDATED_AT = SYSDATE
                WHERE ROLE_ID = :id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = role.RoleId;
            cmd.Parameters.Add("name", OracleDbType.Varchar2).Value = Truncate(role.Name.Trim(), 80);
            cmd.Parameters.Add("description", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(role.Description) ? DBNull.Value : Truncate(role.Description.Trim(), 400);
            cmd.Parameters.Add("is_active", OracleDbType.Char).Value = role.IsActive ? "Y" : "N";
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteRoleAsync(int roleId)
        {
            await using var conn = Open();
            await conn.OpenAsync();
            await using (var delPerm = new OracleCommand(
                "DELETE FROM MOBILE_ADMIN_ROLE_PERM WHERE ROLE_ID = :id", conn))
            {
                delPerm.BindByName = true;
                delPerm.Parameters.Add("id", OracleDbType.Int32).Value = roleId;
                await delPerm.ExecuteNonQueryAsync();
            }

            await using var cmd = new OracleCommand(
                "DELETE FROM MOBILE_ADMIN_ROLE WHERE ROLE_ID = :id AND IS_SYSTEM = 'N'", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = roleId;
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task SetRolePermissionsAsync(int roleId, IReadOnlyList<string> moduleKeys)
        {
            await using var conn = Open();
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();

            await using (var del = new OracleCommand(
                "DELETE FROM MOBILE_ADMIN_ROLE_PERM WHERE ROLE_ID = :id", conn))
            {
                del.Transaction = tx;
                del.BindByName = true;
                del.Parameters.Add("id", OracleDbType.Int32).Value = roleId;
                await del.ExecuteNonQueryAsync();
            }

            foreach (var key in moduleKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await using var ins = new OracleCommand(@"
                    INSERT INTO MOBILE_ADMIN_ROLE_PERM (ROLE_ID, PERMISSION_ID)
                    SELECT :role_id, PERMISSION_ID
                    FROM MOBILE_ADMIN_PERMISSION
                    WHERE UPPER(MODULE_KEY) = UPPER(:module_key)", conn);
                ins.Transaction = tx;
                ins.BindByName = true;
                ins.Parameters.Add("role_id", OracleDbType.Int32).Value = roleId;
                ins.Parameters.Add("module_key", OracleDbType.Varchar2).Value = key.Trim();
                await ins.ExecuteNonQueryAsync();
            }

            await using (var touch = new OracleCommand(
                "UPDATE MOBILE_ADMIN_ROLE SET UPDATED_AT = SYSDATE WHERE ROLE_ID = :id", conn))
            {
                touch.Transaction = tx;
                touch.BindByName = true;
                touch.Parameters.Add("id", OracleDbType.Int32).Value = roleId;
                await touch.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }

        public async Task<List<string>> GetPermissionKeysForRoleAsync(string roleCode)
        {
            if (string.IsNullOrWhiteSpace(roleCode)) return new List<string>();
            await using var conn = Open();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(@"
                SELECT p.MODULE_KEY
                FROM MOBILE_ADMIN_PERMISSION p
                INNER JOIN MOBILE_ADMIN_ROLE_PERM rp ON rp.PERMISSION_ID = p.PERMISSION_ID
                INNER JOIN MOBILE_ADMIN_ROLE r ON r.ROLE_ID = rp.ROLE_ID
                WHERE UPPER(r.CODE) = UPPER(:code)
                  AND r.IS_ACTIVE = 'Y'
                ORDER BY p.SORT_ORDER", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("code", OracleDbType.Varchar2).Value = roleCode.Trim();
            var keys = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var key = reader["MODULE_KEY"]?.ToString();
                if (!string.IsNullOrWhiteSpace(key)) keys.Add(key);
            }

            return keys;
        }

        public async Task<List<AdminStaffUserRecord>> GetStaffUsersAsync()
        {
            var result = new List<AdminStaffUserRecord>();
            await using var conn = Open();
            await using var cmd = new OracleCommand(@"
                SELECT ADMIN_ID, USERNAME, DISPLAY_NAME, ROLE, IS_ACTIVE, CREATED_AT, UPDATED_AT
                FROM MOBILE_ADMIN_USER
                ORDER BY USERNAME ASC", conn);
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(MapStaff(reader));
            }

            return result;
        }

        public async Task<AdminStaffUserRecord?> GetStaffUserByIdAsync(int adminId)
        {
            await using var conn = Open();
            await using var cmd = new OracleCommand(@"
                SELECT ADMIN_ID, USERNAME, DISPLAY_NAME, ROLE, IS_ACTIVE, CREATED_AT, UPDATED_AT
                FROM MOBILE_ADMIN_USER WHERE ADMIN_ID = :id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = adminId;
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return MapStaff(reader);
        }

        public async Task<bool> UsernameExistsAsync(string username, int? excludeAdminId = null)
        {
            await using var conn = Open();
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*) FROM MOBILE_ADMIN_USER
                WHERE UPPER(USERNAME) = UPPER(:username)
                  AND (:exclude_id IS NULL OR ADMIN_ID <> :exclude_id)", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username.Trim();
            cmd.Parameters.Add("exclude_id", OracleDbType.Int32).Value =
                excludeAdminId.HasValue ? excludeAdminId.Value : DBNull.Value;
            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        public async Task<AdminStaffUserRecord> InsertStaffUserAsync(AdminStaffUserRecord user, string passwordHash)
        {
            await using var conn = Open();
            await conn.OpenAsync();
            int id;
            await using (var seq = new OracleCommand("SELECT MOBILE_ADMIN_USER_SEQ.NEXTVAL FROM DUAL", conn))
            {
                id = Convert.ToInt32((await seq.ExecuteScalarAsync())!.ToString());
            }

            await using var cmd = new OracleCommand(@"
                INSERT INTO MOBILE_ADMIN_USER (
                    ADMIN_ID, USERNAME, DISPLAY_NAME, PASSWORD_HASH, ROLE, IS_ACTIVE, CREATED_AT, UPDATED_AT
                ) VALUES (
                    :id, :username, :display_name, :password_hash, :role, :is_active, SYSDATE, SYSDATE
                )", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = id;
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = Truncate(user.Username.Trim(), 80);
            cmd.Parameters.Add("display_name", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(user.DisplayName) ? DBNull.Value : Truncate(user.DisplayName.Trim(), 120);
            cmd.Parameters.Add("password_hash", OracleDbType.Varchar2).Value = passwordHash;
            cmd.Parameters.Add("role", OracleDbType.Varchar2).Value = Truncate(user.Role.Trim(), 40);
            cmd.Parameters.Add("is_active", OracleDbType.Char).Value = user.IsActive ? "Y" : "N";
            await cmd.ExecuteNonQueryAsync();
            return (await GetStaffUserByIdAsync(id))!;
        }

        public async Task<bool> UpdateStaffUserAsync(AdminStaffUserRecord user)
        {
            await using var conn = Open();
            await using var cmd = new OracleCommand(@"
                UPDATE MOBILE_ADMIN_USER
                SET DISPLAY_NAME = :display_name,
                    ROLE = :role,
                    IS_ACTIVE = :is_active,
                    UPDATED_AT = SYSDATE
                WHERE ADMIN_ID = :id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = user.AdminId;
            cmd.Parameters.Add("display_name", OracleDbType.Varchar2).Value =
                string.IsNullOrWhiteSpace(user.DisplayName) ? DBNull.Value : Truncate(user.DisplayName.Trim(), 120);
            cmd.Parameters.Add("role", OracleDbType.Varchar2).Value = Truncate(user.Role.Trim(), 40);
            cmd.Parameters.Add("is_active", OracleDbType.Char).Value = user.IsActive ? "Y" : "N";
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> UpdateStaffPasswordAsync(int adminId, string passwordHash)
        {
            await using var conn = Open();
            await using var cmd = new OracleCommand(@"
                UPDATE MOBILE_ADMIN_USER
                SET PASSWORD_HASH = :password_hash, UPDATED_AT = SYSDATE
                WHERE ADMIN_ID = :id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = adminId;
            cmd.Parameters.Add("password_hash", OracleDbType.Varchar2).Value = passwordHash;
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<int> CountActiveUsersWithRoleAsync(string roleCode)
        {
            await using var conn = Open();
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*) FROM MOBILE_ADMIN_USER
                WHERE UPPER(ROLE) = UPPER(:role) AND IS_ACTIVE = 'Y'", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("role", OracleDbType.Varchar2).Value = roleCode.Trim();
            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public Task EnsureAdminUserTableAsync() => Task.CompletedTask;

        private OracleConnection Open() =>
            new(_configuration.GetConnectionString("HMISConnection"));

        private static async Task<List<string>> GetPermissionKeysForRoleIdAsync(OracleConnection conn, int roleId)
        {
            await using var cmd = new OracleCommand(@"
                SELECT p.MODULE_KEY
                FROM MOBILE_ADMIN_PERMISSION p
                INNER JOIN MOBILE_ADMIN_ROLE_PERM rp ON rp.PERMISSION_ID = p.PERMISSION_ID
                WHERE rp.ROLE_ID = :id
                ORDER BY p.SORT_ORDER", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("id", OracleDbType.Int32).Value = roleId;
            var keys = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var key = reader["MODULE_KEY"]?.ToString();
                if (!string.IsNullOrWhiteSpace(key)) keys.Add(key);
            }

            return keys;
        }

        private static AdminRoleRecord MapRole(OracleDataReader reader) => new()
        {
            RoleId = Convert.ToInt32(reader["ROLE_ID"]),
            Code = reader["CODE"]?.ToString() ?? string.Empty,
            Name = reader["NAME"]?.ToString() ?? string.Empty,
            Description = reader["DESCRIPTION"] == DBNull.Value ? null : reader["DESCRIPTION"]?.ToString(),
            IsSystem = (reader["IS_SYSTEM"]?.ToString() ?? "N") == "Y",
            IsActive = (reader["IS_ACTIVE"]?.ToString() ?? "N") == "Y",
            CreatedAt = reader["CREATED_AT"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(reader["CREATED_AT"]),
            UpdatedAt = reader["UPDATED_AT"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(reader["UPDATED_AT"]),
        };

        private static AdminStaffUserRecord MapStaff(OracleDataReader reader) => new()
        {
            AdminId = Convert.ToInt32(reader["ADMIN_ID"]),
            Username = reader["USERNAME"]?.ToString() ?? string.Empty,
            DisplayName = reader["DISPLAY_NAME"] == DBNull.Value ? null : reader["DISPLAY_NAME"]?.ToString(),
            Role = reader["ROLE"]?.ToString() ?? string.Empty,
            IsActive = (reader["IS_ACTIVE"]?.ToString() ?? "N") == "Y",
            CreatedAt = reader["CREATED_AT"] == DBNull.Value ? null : Convert.ToDateTime(reader["CREATED_AT"]),
            UpdatedAt = reader["UPDATED_AT"] == DBNull.Value ? null : Convert.ToDateTime(reader["UPDATED_AT"]),
        };

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value[..max];
    }
}
