@echo off
REM Run ON the API server (172.20.8.36) as Administrator.
REM Fixes "Access to the path ...\wwwroot\uploads\profiles\... is denied."

set ROOT=C:\inetpub\wwwroot\HMIS_MobileApp_APIs\wwwroot\uploads\profiles
set PROG=C:\ProgramData\BTIH\HospitalMobileAPPApi\uploads\profiles

mkdir "%ROOT%" 2>nul
mkdir "%PROG%" 2>nul

icacls "%ROOT%" /grant "IIS_IUSRS:(OI)(CI)M" /T
icacls "%PROG%" /grant "IIS_IUSRS:(OI)(CI)M" /T
icacls "%ROOT%" /grant "Users:(OI)(CI)M" /T
icacls "%PROG%" /grant "Users:(OI)(CI)M" /T

REM Uncomment and set your real app pool name if different:
REM icacls "%ROOT%" /grant "IIS AppPool\HMIS_MobileApp_APIs:(OI)(CI)M" /T
REM icacls "%PROG%" /grant "IIS AppPool\HMIS_MobileApp_APIs:(OI)(CI)M" /T

echo Done. Retry profile photo upload from the app.
pause
