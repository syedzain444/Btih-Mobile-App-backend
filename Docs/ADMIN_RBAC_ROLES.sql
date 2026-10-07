-- REQ-2026-025 / TC-025 + dynamic RBAC
-- Roles, permissions, and admin users are managed in the Admin Panel:
--   System → Access control (users) / Roles & permissions
--
-- Tables (auto-created by API EnsureAdminRbacSchemaAsync):
--   MOBILE_ADMIN_USER
--   MOBILE_ADMIN_ROLE
--   MOBILE_ADMIN_PERMISSION
--   MOBILE_ADMIN_ROLE_PERM
--
-- Default seeded roles:
--   Admin      — full access including AccessControl
--   Staff      — operations (refills, tickets, promotions, offers, help)
--   Reception  — dashboard, messages, appointments
--
-- Prefer managing users/roles in the UI. SQL examples below are for emergencies only.

-- Promote an existing user to Admin:
-- UPDATE MOBILE_ADMIN_USER
--    SET ROLE = 'Admin', IS_ACTIVE = 'Y'
--  WHERE UPPER(USERNAME) = UPPER('admin');

-- Set Reception for TC-025 testing:
-- UPDATE MOBILE_ADMIN_USER
--    SET ROLE = 'Reception', IS_ACTIVE = 'Y'
--  WHERE UPPER(USERNAME) = UPPER('reception');

-- After changing ROLE in SQL, the user must log out and log in again
-- so the session picks up the new permission list.
