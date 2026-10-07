using System.Collections.Concurrent;
using HospitalMobileAPPApi.Configuration;
using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;
using Microsoft.Extensions.Options;

namespace HospitalMobileAPPApi.Services
{
    public interface IRbacService
    {
        Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
        Task<List<AdminPermissionRecord>> GetPermissionsAsync();
        Task<List<AdminRoleRecord>> GetRolesAsync(bool includeInactive = true);
        Task<AdminRoleRecord?> GetRoleAsync(int roleId);
        Task<AdminRoleRecord> CreateRoleAsync(CreateAdminRoleRequest request);
        Task<AdminRoleRecord?> UpdateRoleAsync(int roleId, UpdateAdminRoleRequest request);
        Task<(bool Success, string Message)> DeleteRoleAsync(int roleId);
        Task<AdminRoleRecord?> SetRolePermissionsAsync(int roleId, IReadOnlyList<string> permissions);
        Task<List<string>> GetModulesForRoleAsync(string roleCode);
        Task<bool> RoleHasModuleAsync(string? roleCode, string moduleKey);
        Task InvalidateCacheAsync();
        Task<List<AdminStaffUserRecord>> GetStaffUsersAsync();
        Task<AdminStaffUserRecord?> GetStaffUserAsync(int adminId);
        Task<AdminStaffUserRecord> CreateStaffUserAsync(CreateAdminStaffRequest request);
        Task<AdminStaffUserRecord?> UpdateStaffUserAsync(int adminId, UpdateAdminStaffRequest request);
        Task<(bool Success, string Message)> ResetStaffPasswordAsync(int adminId, string newPassword);
    }

    public class RbacService : IRbacService
    {
        private static readonly ConcurrentDictionary<string, (DateTime LoadedAt, List<string> Modules)> Cache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

        private readonly IRbacRepository _repository;
        private readonly IMobilePortalSchemaService _schemaService;
        private readonly AdminSettings _adminSettings;

        public RbacService(
            IRbacRepository repository,
            IMobilePortalSchemaService schemaService,
            IOptions<AdminSettings> adminSettings)
        {
            _repository = repository;
            _schemaService = schemaService;
            _adminSettings = adminSettings.Value;
        }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) =>
            _schemaService.EnsureAdminRbacSchemaAsync(cancellationToken);

        public async Task<List<AdminPermissionRecord>> GetPermissionsAsync()
        {
            await EnsureSchemaAsync();
            return await _repository.GetPermissionsAsync();
        }

        public async Task<List<AdminRoleRecord>> GetRolesAsync(bool includeInactive = true)
        {
            await EnsureSchemaAsync();
            return await _repository.GetRolesAsync(includeInactive);
        }

        public async Task<AdminRoleRecord?> GetRoleAsync(int roleId)
        {
            await EnsureSchemaAsync();
            return await _repository.GetRoleByIdAsync(roleId);
        }

        public async Task<AdminRoleRecord> CreateRoleAsync(CreateAdminRoleRequest request)
        {
            await EnsureSchemaAsync();
            var code = SanitizeRoleCode(request.Code);
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException("Role code is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new InvalidOperationException("Role name is required.");
            }

            if (await _repository.GetRoleByCodeAsync(code) != null)
            {
                throw new InvalidOperationException($"Role code '{code}' already exists.");
            }

            var created = await _repository.InsertRoleAsync(new AdminRoleRecord
            {
                Code = code,
                Name = request.Name.Trim(),
                Description = request.Description,
                IsActive = request.IsActive,
            });

            if (request.Permissions != null)
            {
                await _repository.SetRolePermissionsAsync(created.RoleId, request.Permissions);
                await InvalidateCacheAsync();
                return (await _repository.GetRoleByIdAsync(created.RoleId))!;
            }

            await InvalidateCacheAsync();
            return created;
        }

        public async Task<AdminRoleRecord?> UpdateRoleAsync(int roleId, UpdateAdminRoleRequest request)
        {
            await EnsureSchemaAsync();
            var existing = await _repository.GetRoleByIdAsync(roleId);
            if (existing == null) return null;
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new InvalidOperationException("Role name is required.");
            }

            // Never deactivate the last active Admin system role with active users without care —
            // still allow rename/description.
            if (existing.IsSystem &&
                string.Equals(existing.Code, AppRoles.Admin, StringComparison.OrdinalIgnoreCase) &&
                !request.IsActive)
            {
                throw new InvalidOperationException("The built-in Admin role cannot be deactivated.");
            }

            existing.Name = request.Name.Trim();
            existing.Description = request.Description;
            existing.IsActive = request.IsActive;
            await _repository.UpdateRoleAsync(existing);
            await InvalidateCacheAsync();
            return await _repository.GetRoleByIdAsync(roleId);
        }

        public async Task<(bool Success, string Message)> DeleteRoleAsync(int roleId)
        {
            await EnsureSchemaAsync();
            var existing = await _repository.GetRoleByIdAsync(roleId);
            if (existing == null) return (false, "Role not found.");
            if (existing.IsSystem) return (false, "System roles cannot be deleted.");

            var users = await _repository.CountActiveUsersWithRoleAsync(existing.Code);
            if (users > 0)
            {
                return (false, $"Cannot delete role '{existing.Code}' while {users} active user(s) still use it.");
            }

            var deleted = await _repository.DeleteRoleAsync(roleId);
            await InvalidateCacheAsync();
            return deleted ? (true, "Role deleted.") : (false, "Role not found or is a system role.");
        }

        public async Task<AdminRoleRecord?> SetRolePermissionsAsync(int roleId, IReadOnlyList<string> permissions)
        {
            await EnsureSchemaAsync();
            var existing = await _repository.GetRoleByIdAsync(roleId);
            if (existing == null) return null;

            var catalog = await _repository.GetPermissionsAsync();
            var valid = permissions
                .Where(p => catalog.Any(c => string.Equals(c.ModuleKey, p, StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Admin system role must always keep AccessControl
            if (existing.IsSystem &&
                string.Equals(existing.Code, AppRoles.Admin, StringComparison.OrdinalIgnoreCase) &&
                !valid.Any(p => string.Equals(p, AdminModules.AccessControl, StringComparison.OrdinalIgnoreCase)))
            {
                valid.Add(AdminModules.AccessControl);
            }

            await _repository.SetRolePermissionsAsync(roleId, valid);
            await InvalidateCacheAsync();
            return await _repository.GetRoleByIdAsync(roleId);
        }

        public async Task<List<string>> GetModulesForRoleAsync(string roleCode)
        {
            if (string.IsNullOrWhiteSpace(roleCode)) return new List<string>();

            if (Cache.TryGetValue(roleCode.Trim(), out var cached) &&
                DateTime.UtcNow - cached.LoadedAt < CacheTtl)
            {
                return cached.Modules.ToList();
            }

            await EnsureSchemaAsync();
            var modules = await _repository.GetPermissionKeysForRoleAsync(roleCode.Trim());

            // Fallback to hardcoded matrix if role has no DB permissions yet
            if (modules.Count == 0)
            {
                modules = FallbackModules(roleCode);
            }

            Cache[roleCode.Trim()] = (DateTime.UtcNow, modules);
            return modules.ToList();
        }

        public async Task<bool> RoleHasModuleAsync(string? roleCode, string moduleKey)
        {
            if (string.IsNullOrWhiteSpace(roleCode) || string.IsNullOrWhiteSpace(moduleKey))
            {
                return false;
            }

            var modules = await GetModulesForRoleAsync(roleCode);
            return modules.Any(m => string.Equals(m, moduleKey, StringComparison.OrdinalIgnoreCase));
        }

        public Task InvalidateCacheAsync()
        {
            Cache.Clear();
            return Task.CompletedTask;
        }

        public async Task<List<AdminStaffUserRecord>> GetStaffUsersAsync()
        {
            await EnsureSchemaAsync();
            var users = await _repository.GetStaffUsersAsync();
            foreach (var user in users)
            {
                user.Permissions = await GetModulesForRoleAsync(user.Role);
            }

            return users;
        }

        public async Task<AdminStaffUserRecord?> GetStaffUserAsync(int adminId)
        {
            await EnsureSchemaAsync();
            var user = await _repository.GetStaffUserByIdAsync(adminId);
            if (user == null) return null;
            user.Permissions = await GetModulesForRoleAsync(user.Role);
            return user;
        }

        public async Task<AdminStaffUserRecord> CreateStaffUserAsync(CreateAdminStaffRequest request)
        {
            await EnsureSchemaAsync();
            var username = request.Username?.Trim() ?? string.Empty;
            if (username.Length < 3)
            {
                throw new InvalidOperationException("Username must be at least 3 characters.");
            }

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            {
                throw new InvalidOperationException("Password must be at least 8 characters.");
            }

            var roleCode = await ResolveActiveRoleCodeAsync(request.Role);
            if (await _repository.UsernameExistsAsync(username))
            {
                throw new InvalidOperationException("Username already exists.");
            }

            var hash = PasswordHasher.Hash(username, request.Password, _adminSettings.PasswordSalt);
            var created = await _repository.InsertStaffUserAsync(new AdminStaffUserRecord
            {
                Username = username,
                DisplayName = request.DisplayName,
                Role = roleCode,
                IsActive = request.IsActive,
            }, hash);

            created.Permissions = await GetModulesForRoleAsync(created.Role);
            return created;
        }

        public async Task<AdminStaffUserRecord?> UpdateStaffUserAsync(int adminId, UpdateAdminStaffRequest request)
        {
            await EnsureSchemaAsync();
            var existing = await _repository.GetStaffUserByIdAsync(adminId);
            if (existing == null) return null;

            var roleCode = await ResolveActiveRoleCodeAsync(request.Role);

            // Prevent removing last active Admin
            if (string.Equals(existing.Role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase) &&
                existing.IsActive &&
                (!request.IsActive || !string.Equals(roleCode, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)))
            {
                var admins = await _repository.CountActiveUsersWithRoleAsync(AppRoles.Admin);
                if (admins <= 1)
                {
                    throw new InvalidOperationException("Cannot remove or deactivate the last active Admin user.");
                }
            }

            existing.DisplayName = request.DisplayName;
            existing.Role = roleCode;
            existing.IsActive = request.IsActive;
            await _repository.UpdateStaffUserAsync(existing);
            return await GetStaffUserAsync(adminId);
        }

        public async Task<(bool Success, string Message)> ResetStaffPasswordAsync(int adminId, string newPassword)
        {
            await EnsureSchemaAsync();
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                return (false, "Password must be at least 8 characters.");
            }

            var user = await _repository.GetStaffUserByIdAsync(adminId);
            if (user == null) return (false, "User not found.");

            var hash = PasswordHasher.Hash(user.Username, newPassword, _adminSettings.PasswordSalt);
            var ok = await _repository.UpdateStaffPasswordAsync(adminId, hash);
            return ok ? (true, "Password updated.") : (false, "Could not update password.");
        }

        private async Task<string> ResolveActiveRoleCodeAsync(string? role)
        {
            var code = string.IsNullOrWhiteSpace(role) ? AppRoles.Staff : role.Trim();
            var record = await _repository.GetRoleByCodeAsync(code);
            if (record == null)
            {
                throw new InvalidOperationException($"Role '{code}' does not exist. Create it under Roles first.");
            }

            if (!record.IsActive)
            {
                throw new InvalidOperationException($"Role '{code}' is inactive.");
            }

            return record.Code;
        }

        private static string SanitizeRoleCode(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var cleaned = new string(raw.Trim()
                .Where(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-')
                .ToArray());
            if (cleaned.Length == 0) return string.Empty;
            return char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
        }

        private static List<string> FallbackModules(string roleCode)
        {
            // Mirror hardcoded AdminModulePermissions matrix
            var admin = new[]
            {
                AdminModules.Dashboard, AdminModules.Messages, AdminModules.Appointments,
                AdminModules.Users, AdminModules.Reports, AdminModules.Analytics, AdminModules.Audit,
                AdminModules.Refills, AdminModules.Tickets, AdminModules.Promotions, AdminModules.Offers,
                AdminModules.SupportContent, AdminModules.AccessControl,
            };
            var staff = new[]
            {
                AdminModules.Dashboard, AdminModules.Messages, AdminModules.Appointments,
                AdminModules.Refills, AdminModules.Tickets, AdminModules.Promotions, AdminModules.Offers,
                AdminModules.SupportContent,
            };
            var reception = new[]
            {
                AdminModules.Dashboard, AdminModules.Messages, AdminModules.Appointments,
            };

            if (string.Equals(roleCode, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
                return admin.ToList();
            if (string.Equals(roleCode, AppRoles.Staff, StringComparison.OrdinalIgnoreCase))
                return staff.ToList();
            if (string.Equals(roleCode, AppRoles.Reception, StringComparison.OrdinalIgnoreCase))
                return reception.ToList();
            return new List<string>();
        }
    }
}
