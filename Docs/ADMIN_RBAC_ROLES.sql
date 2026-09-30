-- REQ-2026-025 / TC-025: Admin panel RBAC roles
-- Roles: Admin (full), Staff (operations), Reception (front desk — no Users/Reports)
--
-- Create or update a Reception user (adjust password hash via API bootstrap or PasswordHasher).
-- Prefer creating via POST /api/admin/auth/bootstrap with Role = "Reception" when secret is set.
--
-- Example: set an existing user to Reception for testing TC-025:

-- UPDATE MOBILE_ADMIN_USER
--    SET ROLE = 'Reception',
--        IS_ACTIVE = 'Y'
--  WHERE UPPER(USERNAME) = UPPER('reception');

-- Example: set an existing user to Admin:

-- UPDATE MOBILE_ADMIN_USER
--    SET ROLE = 'Admin',
--        IS_ACTIVE = 'Y'
--  WHERE UPPER(USERNAME) = UPPER('admin');

-- Example: set operational staff:

-- UPDATE MOBILE_ADMIN_USER
--    SET ROLE = 'Staff',
--        IS_ACTIVE = 'Y'
--  WHERE UPPER(USERNAME) = UPPER('staff');

-- Permission matrix (menus + API):
-- Dashboard / Messages / Appointments : Admin, Staff, Reception
-- Users / Reports / Analytics / Audit : Admin only
-- Refills / Tickets / Promotions / Help Content : Admin, Staff
