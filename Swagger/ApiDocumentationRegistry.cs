namespace HospitalMobileAPPApi.Swagger
{
    internal static class ApiDocumentationRegistry
    {
        public static IReadOnlyDictionary<string, ApiDocEntry> Operations { get; } =
            new Dictionary<string, ApiDocEntry>(StringComparer.Ordinal)
            {
                ["Auth_Login"] = new(
                    summary: "Patient login (hybrid trusted device)",
                    description: """
                        Authenticates a patient using contact number and password.

                        **Trusted device:** If `deviceInstallId` + `deviceTrustToken` match a stored trusted device, returns JWT immediately.

                        **New device:** If credentials are valid but the device is not trusted, returns `requiresOtp: true` and sends a verification code. Complete login with `POST /api/Auth/verify-login-otp`.

                        Omit `deviceInstallId` for legacy/staff direct login behaviour.
                        """,
                    requestExample: """
                        {
                          "contactNo": "03001234567",
                          "password": "yourPassword",
                          "deviceInstallId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                          "deviceTrustToken": "optional-if-already-trusted",
                          "deviceLabel": "Samsung Galaxy S24",
                          "platform": "android"
                        }
                        """,
                    responseExample: """
                        {
                          "success": true,
                          "requiresOtp": false,
                          "message": "Login successful",
                          "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
                          "tokenType": "Bearer",
                          "expiresAt": "2026-03-08T18:30:00Z",
                          "expiresInSeconds": 5700,
                          "mrNo": "010-002-152",
                          "firstName": "Ali"
                        }
                        """),

                ["Auth_VerifyLoginOtp"] = new(
                    summary: "Verify login OTP and trust device",
                    description: """
                        Completes login for a new/unrecognized device after `POST /api/Auth/login` returned `requiresOtp: true`.
                        Optionally registers the device as trusted and returns a new `deviceTrustToken` for the mobile app to store securely.
                        """,
                    requestExample: """
                        {
                          "loginChallengeId": "f3c2...",
                          "otp": "123456",
                          "deviceInstallId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                          "deviceLabel": "Samsung Galaxy S24",
                          "platform": "android",
                          "trustDevice": true
                        }
                        """),

                ["TrustedDevice_GetTrustedDevices"] = new(
                    summary: "List trusted login devices",
                    description: "Returns active trusted devices for password-only login. Requires JWT.",
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["mrNo"] = "Patient MR number.",
                    }),

                ["TrustedDevice_RevokeTrustedDevice"] = new(
                    summary: "Revoke one trusted device",
                    description: "Removes trust for a single device. Next login from that device requires OTP."),

                ["TrustedDevice_RevokeAllTrustedDevices"] = new(
                    summary: "Revoke all trusted devices",
                    description: "Removes trust from every device for the patient."),

                ["Auth_VerifyNumber"] = new(
                    summary: "Verify phone number or MR number",
                    description: """
                        Validates that a contact number or MR number exists in HMIS before forgot-password or registration flows.
                        Provide at least one of `ContactNo` or `mrno` as query parameters.
                        """,
                    responseExample: """
                        {
                          "message": "Verification successful",
                          "mr_no": "010-002-152",
                          "contactno": "03001234567"
                        }
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["ContactNo"] = "Registered patient mobile number (e.g. 03001234567).",
                        ["mrno"] = "Patient MR number (e.g. 010-002-152).",
                    }),

                ["Auth_SendOtp"] = new(
                    summary: "Send OTP via SMS",
                    description: """
                        Sends a 6-digit OTP to a **registered** phone number. OTP expires in 2 minutes (configurable).
                        Used in the forgot-password flow before `verify-otp`.
                        """,
                    responseExample: """
                        {
                          "success": true,
                          "message": "OTP sent successfully",
                          "expiresInMinutes": 2
                        }
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["phoneNumber"] = "Registered patient mobile number.",
                    }),

                ["Auth_VerifyOtp"] = new(
                    summary: "Verify OTP",
                    description: """
                        Validates the OTP sent to the patient's phone. On success, opens a short password-reset window (5 minutes).
                        Required before calling `updatePassword`.
                        """,
                    responseExample: """
                        {
                          "success": true,
                          "message": "OTP verified successfully",
                          "mrNo": "010-002-152"
                        }
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["phoneNumber"] = "Same phone number used in send-otp.",
                        ["otp"] = "6-digit OTP received by SMS.",
                    }),

                ["Auth_UpdatePassword"] = new(
                    summary: "Reset password (JSON body)",
                    description: """
                        Updates patient password after successful OTP verification (`verify-otp`).
                        Alternative to `POST /api/Patient/updatePassword` (query-string version used by mobile app).
                        """,
                    requestExample: """
                        {
                          "mrNo": "010-002-152",
                          "patientPassword": "newSecurePassword123"
                        }
                        """,
                    responseExample: """
                        {
                          "message": "Password updated successfully"
                        }
                        """),

                ["Doctor_GetDoctors"] = new(
                    summary: "Get paginated doctor list",
                    description: "Returns active doctors with pagination metadata. No authentication required.",
                    responseExample: """
                        {
                          "data": [
                            {
                              "doctorId": 101,
                              "doctorName": "Dr. Ahmed Khan",
                              "specialization": "Cardiology",
                              "departmentId": 5
                            }
                          ],
                          "pagination": {
                            "pageNumber": 1,
                            "pageSize": 10,
                            "totalRecords": 45,
                            "totalPages": 5
                          }
                        }
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["pageNumber"] = "Page number (default: 1).",
                        ["pageSize"] = "Records per page (default: 10).",
                    }),

                ["Doctor_GetDoctorsSched"] = new(
                    summary: "Get doctor OPD schedule",
                    description: "Returns weekly schedule slots for a doctor by `doctorId`.",
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["doctorId"] = "Doctor ID from the doctor list.",
                    }),

                ["Doctor_GetDoctorSpecialization"] = new(
                    summary: "Get all specializations",
                    description: "Returns distinct doctor specializations/departments for filters in the mobile app."),

                ["Patient_GetPatient"] = new(
                    summary: "Get patient profile and visit history",
                    description: """
                        Returns patient demographics plus paginated visit history.
                        Requires JWT Bearer token.

                        `visitHistory` contains the current page; use `visitHistoryPagination` for total counts.
                        """,
                    responseExample: """
                        {
                          "profile": {
                            "mrNo": "010-002-152",
                            "firstName": "Ali",
                            "lastName": "Khan",
                            "gender": "M",
                            "dateOfBirth": "1990-05-15T00:00:00",
                            "cnic": "42101-1234567-1",
                            "contactNo": "03001234567",
                            "bloodGroup": "O+",
                            "emailAddress": "ali@example.com"
                          },
                          "visitHistory": [
                            {
                              "serialNumber": 1,
                              "patientVisitId": 12345,
                              "visitDate": "2026-03-01T10:30:00",
                              "doctorName": "Dr. Ahmed",
                              "department": "Cardiology",
                              "isDischarged": true
                            }
                          ],
                          "visitHistoryPagination": {
                            "pageNumber": 1,
                            "pageSize": 20,
                            "totalRecords": 45,
                            "totalPages": 3
                          }
                        }
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["MR_NO"] = "Patient MR number.",
                        ["visitPageNumber"] = "Visit history page (default: 1).",
                        ["visitPageSize"] = "Visit history page size (default: 20, max: 100).",
                    }),

                ["Patient_GetLaboratoryReports"] = new(
                    summary: "Get laboratory reports list",
                    description: "Returns lab reports with status `REPORT GENERATED` for the patient.",
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["MR_NO"] = "Patient MR number.",
                    }),

                ["Patient_GetGastroReports"] = new(
                    summary: "Get gastro/endoscopy reports list",
                    description: "Returns gastro and endoscopy reports (TESTTYPE GASTRO/ENDOSCOPY).",
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["MR_NO"] = "Patient MR number.",
                    }),

                ["Patient_GetRadiology"] = new(
                    summary: "Get radiology reports list",
                    description: "Returns radiology reports excluding laboratory and gastro types.",
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["MR_NO"] = "Patient MR number.",
                    }),

                ["Patient_GetReportFromWebsite"] = new(
                    summary: "Proxy PDF from hospital website",
                    description: "Fetches a report PDF from btkhospital.com by report id. Returns `application/pdf`.",
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["id"] = "Report id on the hospital website.",
                    }),

                ["Patient_GetPrescriptions"] = new(
                    summary: "Get prescription visit list",
                    description: """
                        Returns distinct prescription visits (doctor, department, visit date) for the patient.
                        Use `patientVisitId` with `/api/PatientReport/prescription/{id}` or `GenerateReport` for PDF.
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["MR_NO"] = "Patient MR number.",
                    }),

                ["Patient_InsertAppointment"] = new(
                    summary: "Book appointment (guest or logged-in)",
                    description: """
                        Creates an appointment request. **No JWT required** — supports guest booking.
                        `mrno` is optional for guests; `name` and `phoneNo` are required.
                        Sends SMS confirmation on success.
                        """,
                    requestExample: """
                        {
                          "name": "Ali Khan",
                          "phoneNo": "03001234567",
                          "mrno": "010-002-152",
                          "email": "ali@example.com",
                          "weekId": 12,
                          "appointment_time": "10:00 AM",
                          "status": "Pending",
                          "doctorId": 101,
                          "departmentId": 5,
                          "purpose": "Follow-up consultation",
                          "createdAt": "2026-03-08T14:00:00",
                          "isActive": "Y",
                          "entryDate": "2026-03-08T14:00:00"
                        }
                        """,
                    responseExample: """
                        {
                          "message": "Appointment requested successfully",
                          "rowsAffected": 1
                        }
                        """),

                ["Patient_UpdateProfile"] = new(
                    summary: "Update patient profile",
                    description: "Updates PATIENT_MST and PATIENT_INFORMATION. Triggers profile-updated push notification.",
                    requestExample: """
                        {
                          "mrNo": "010-002-152",
                          "firstName": "Ali",
                          "lastName": "Khan",
                          "gender": "M",
                          "dateOfBirth": "1990-05-15T00:00:00",
                          "cnic": "42101-1234567-1",
                          "contactNo": "03001234567",
                          "bloodGroup": "O+",
                          "emailAddress": "ali@example.com"
                        }
                        """,
                    responseExample: """
                        {
                          "message": "Profile updated successfully",
                          "profile": {
                            "mrNo": "010-002-152",
                            "firstName": "Ali",
                            "lastName": "Khan"
                          }
                        }
                        """),

                ["Patient_UpdatePassword"] = new(
                    summary: "Reset password (query params — mobile app)",
                    description: """
                        Updates password using query parameters. Requires prior OTP verification via `/api/Auth/verify-otp`.
                        **No JWT required** but reset session must be active (5 minutes after OTP verify).
                        """,
                    responseExample: """
                        {
                          "message": "Password updated successfully"
                        }
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["mrno"] = "Patient MR number.",
                        ["patientPassword"] = "New password (minimum 6 characters).",
                    }),

                ["Patient_GetAppointments"] = new(
                    summary: "Get patient appointments",
                    description: "Returns appointment history for the patient from the web appointment database.",
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["mrno"] = "Patient MR number.",
                    }),

                ["Patient_GetDischargeHistory"] = new(
                    summary: "Get paginated discharge history",
                    description: """
                        Returns discharged visits with pagination. Response headers include
                        `X-Page-Number`, `X-Page-Size`, and `X-Total-Count`.
                        """,
                    responseExample: """
                        {
                          "pageNumber": 1,
                          "pageSize": 10,
                          "totalRecords": 25,
                          "totalPages": 3,
                          "data": [
                            {
                              "mr_NO": "010-002-152",
                              "patient_VISIT_ID": 12345,
                              "check_IN": "2026-02-01T09:00:00",
                              "dR_OUT": "2026-02-03T11:00:00",
                              "doctor_NAME": "Dr. Ahmed",
                              "admission_OFFICER": "Sara"
                            }
                          ]
                        }
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["mrno"] = "Patient MR number.",
                        ["pageNumber"] = "Page number (default: 1).",
                        ["pageSize"] = "Page size (default: 10, max: 100).",
                    }),

                ["PatientReport_GenerateReport"] = new(
                    summary: "Generate lab/gastro/radiology/prescription PDF",
                    description: """
                        Generates a PDF report using RDLC templates from the `Reports` folder.
                        Used by the mobile app for health record PDFs.

                        | reportName | rptId | parameters |
                        |------------|-------|------------|
                        | Labrpt | 19 | PAT_DIAG_ID |
                        | GastRpt | 64 | PAT_DIAG_ID |
                        | RadRpt | 22 | PAT_DIAG_ID |
                        | PRESCRIPTION_A4 | 141 | PATIENT_VISIT_ID |
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["rptId"] = "Report master id (19=Lab, 64=Gastro, 22=Radiology, 141=Prescription).",
                        ["reportName"] = "RDLC file name without extension (e.g. Labrpt).",
                        ["parameters"] = "Primary key — PAT_DIAG_ID or PATIENT_VISIT_ID depending on report.",
                        ["user"] = "Printed-by label (default: MobileApp).",
                    }),

                ["PatientReport_GeneratePrescriptionReport"] = new(
                    summary: "Generate prescription PDF (shortcut)",
                    description: "Shortcut for prescription PDF — equivalent to `GenerateReport?rptId=141&reportName=PRESCRIPTION_A4&parameters={visitId}`.",
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["patientVisitId"] = "PATIENT_VISIT_ID from prescription list.",
                    }),

                ["Billing_GetOverview"] = new(
                    summary: "Billing overview",
                    description: """
                        Returns billing dashboard totals for the mobile app overview screen:
                        total bills, paid/pending amounts, and per-department counts.

                        Backed by Oracle `SP_BILL_PAY` (COND=40).
                        """,
                    responseExample: """
                        {
                          "mrNo": "010-002-152",
                          "totalBillCount": 12,
                          "totalAmount": 45000.00,
                          "paidBillCount": 10,
                          "paidAmount": 42000.00,
                          "pendingBillCount": 2,
                          "pendingAmount": 3000.00,
                          "cancelledBillCount": 0,
                          "departments": [
                            {
                              "departmentCode": "LABORATORY",
                              "departmentName": "Laboratory",
                              "billCount": 4,
                              "totalAmount": 8000.00,
                              "reportId": 26
                            }
                          ]
                        }
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["mrNo"] = "Patient MR number.",
                    }),

                ["Billing_GetHistory"] = new(
                    summary: "Payment history",
                    description: """
                        Returns filtered bill/payment history for the mobile payment history screen.
                        Supports department, year, date range, search, and payment status filters.
                        """,
                    responseExample: """
                        {
                          "mrNo": "010-002-152",
                          "totalCount": 2,
                          "totalAmount": 5000.00,
                          "items": [
                            {
                              "billId": "123456",
                              "invoiceNo": "INV-2026-001",
                              "department": "Laboratory",
                              "departmentCode": "LABORATORY",
                              "paymentDate": "2026-03-01T10:00:00",
                              "amount": 2500.00,
                              "paymentStatus": "paid",
                              "reportId": 26
                            }
                          ]
                        }
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["mrNo"] = "Patient MR number.",
                        ["department"] = "Optional department code (EMERGENCY, OPD, LABORATORY, etc.).",
                        ["year"] = "Optional payment/visit year filter.",
                        ["dateFrom"] = "Optional start date (inclusive).",
                        ["dateTo"] = "Optional end date (inclusive).",
                        ["search"] = "Optional search on bill ID, invoice, department, or amount.",
                        ["paymentStatus"] = "Optional filter: paid, pending, or cancelled.",
                    }),

                ["Billing_GetInvoice"] = new(
                    summary: "Invoice details",
                    description: "Returns a single invoice/bill record for the invoice detail screen.",
                    responseExample: """
                        {
                          "billId": "123456",
                          "mrNo": "010-002-152",
                          "invoiceNo": "INV-2026-001",
                          "department": "Laboratory",
                          "departmentCode": "LABORATORY",
                          "visitDate": "2026-02-28T09:30:00",
                          "paymentDate": "2026-03-01T10:00:00",
                          "paymentMethod": "CASH",
                          "amount": 2500.00,
                          "paymentStatus": "paid",
                          "reportId": 26
                        }
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["billId"] = "Bill ID from history/overview.",
                        ["mrNo"] = "Patient MR number (required query parameter).",
                    }),

                ["Billing_GetPaymentSummary"] = new(
                    summary: "Payment screen summary",
                    description: """
                        Returns pending balances and recent payments for the mobile payment screen.
                        Online payment processing is not included — this is read-only HMIS billing data.
                        """,
                    responseExample: """
                        {
                          "mrNo": "010-002-152",
                          "totalPaidAmount": 42000.00,
                          "totalPendingAmount": 3000.00,
                          "paidBillCount": 10,
                          "pendingBillCount": 2,
                          "pendingBills": [],
                          "recentPayments": []
                        }
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["mrNo"] = "Patient MR number.",
                    }),

                ["PatientReport_GetBillingHistory"] = new(
                    summary: "Get billing history (legacy)",
                    description: """
                        **Deprecated** — prefer `GET /api/Billing/history/{mrNo}`.

                        Returns a flat array of bills for backward compatibility with older app builds.
                        """,
                    responseExample: """
                        [
                          {
                            "billId": "123456",
                            "invoiceNo": "INV-2026-001",
                            "department": "Laboratory",
                            "paymentDate": "2026-03-01T10:00:00",
                            "amount": 2500.00
                          }
                        ]
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["mrNo"] = "Patient MR number.",
                    }),

                ["PatientReport_GenerateBillingReport"] = new(
                    summary: "Generate billing department PDF",
                    description: """
                        Generates billing PDF by department. Used by billing history screen in the mobile app.

                        | Department | rptId |
                        |------------|-------|
                        | OPD / Services / Laboratory / Radiology / Procedure | 26 |
                        | Emergency / IPD | 27 |

                        Pass `billId` as `param` for individual bill PDF, or patient MR for summary.
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["rptId"] = "Report id (26 or 27 — see description).",
                        ["param"] = "Bill ID or MR number depending on report type.",
                        ["empId"] = "Employee id for 'Printed by' (optional, defaults to Security:MobileAppEmpId).",
                        ["d1"] = "Optional start date for date-range reports.",
                        ["d2"] = "Optional end date for date-range reports.",
                    }),

                ["PatientReport_GenerateDischargeReport"] = new(
                    summary: "Generate discharge summary PDF",
                    description: """
                        Generates discharge report PDF for a patient visit.
                        Resolves discharge id from `PATIENT_VISIT_ID` automatically.
                        Default `rptId=35` in mobile app.
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["rptId"] = "Discharge report master id (default in app: 35).",
                        ["param"] = "PATIENT_VISIT_ID from discharge history.",
                        ["empId"] = "Employee id for 'Printed by' (optional).",
                        ["d1"] = "Optional start date.",
                        ["d2"] = "Optional end date.",
                    }),

                ["PushNotification_RegisterDeviceToken"] = new(
                    summary: "Register FCM device token",
                    description: """
                        Registers the device's FCM token for push notifications. Called after login.
                        Requires JWT Bearer token.
                        """,
                    requestExample: """
                        {
                          "mrNo": "010-002-152",
                          "deviceToken": "fcm-device-token-from-firebase",
                          "platform": "android"
                        }
                        """,
                    responseExample: """
                        {
                          "message": "Device token registered successfully"
                        }
                        """),

                ["PushNotification_UnregisterDeviceToken"] = new(
                    summary: "Unregister FCM device token",
                    description: "Removes device token on logout. Requires JWT Bearer token.",
                    requestExample: """
                        {
                          "mrNo": "010-002-152",
                          "deviceToken": "fcm-device-token-from-firebase"
                        }
                        """,
                    responseExample: """
                        {
                          "message": "Device token unregistered successfully"
                        }
                        """),

                ["PushNotification_SendAppointmentReminder"] = new(
                    summary: "Send appointment reminder push",
                    description: """
                        Sends FCM push to all registered devices for the patient.
                        Intended for hospital/admin/scheduled jobs — not called by the mobile app directly.
                        """,
                    requestExample: """
                        {
                          "mrNo": "010-002-152",
                          "title": "Appointment Reminder",
                          "body": "Your appointment with Dr. Ahmed is tomorrow at 10:00 AM",
                          "appointmentId": "12345"
                        }
                        """,
                    responseExample: """
                        {
                          "message": "Appointment reminder processed",
                          "result": {
                            "sent": 1,
                            "failed": 0,
                            "totalTokens": 1,
                            "errors": []
                          }
                        }
                        """),

                ["PushNotification_SendReportReadyAlert"] = new(
                    summary: "Send report-ready push",
                    description: """
                        Sends FCM push when a lab/radiology report is ready.
                        Intended for hospital/admin systems — not called by the mobile app directly.
                        """,
                    requestExample: """
                        {
                          "mrNo": "010-002-152",
                          "title": "Report Ready",
                          "body": "Your lab report is now available",
                          "reportType": "lab",
                          "reportId": "67890"
                        }
                        """,
                    responseExample: """
                        {
                          "message": "Report-ready alert processed",
                          "result": {
                            "sent": 1,
                            "failed": 0,
                            "totalTokens": 1,
                            "errors": []
                          }
                        }
                        """),

                ["Auth_SendRegistrationOtp"] = new(
                    summary: "Send registration OTP",
                    description: """
                        Sends a 6-digit OTP to any phone number for the **new account registration** flow.
                        Unlike `send-otp`, this works for phones not yet in HMIS.

                        **Flow:** `send-registration-otp` → `register` with OTP → `setup` profile if required.
                        """,
                    responseExample: """
                        {
                          "success": true,
                          "message": "Registration OTP sent successfully",
                          "expiresInMinutes": 2
                        }
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["phoneNumber"] = "Mobile number to register (e.g. 03001234567).",
                    }),

                ["Auth_Register"] = new(
                    summary: "Register new patient portal account",
                    description: """
                        Creates a portal account after OTP verification.

                        - **Existing HMIS patient:** links phone/MR and sets portal password.
                        - **New patient:** creates a provisional MR (`MOB-00000001`) until linked to HMIS.

                        Returns JWT token immediately — navigate to profile setup when `profileSetupRequired` is true.
                        """,
                    requestExample: """
                        {
                          "phoneNumber": "03001234567",
                          "firstName": "Ali",
                          "lastName": "Khan",
                          "password": "secret123",
                          "confirmPassword": "secret123",
                          "acceptTerms": true,
                          "otp": "123456"
                        }
                        """,
                    responseExample: """
                        {
                          "success": true,
                          "message": "Registration successful",
                          "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
                          "tokenType": "Bearer",
                          "expiresAt": "2026-03-08T18:30:00Z",
                          "expiresInSeconds": 5700,
                          "mrNo": "010-002-152",
                          "firstName": "Ali",
                          "profileSetupRequired": true,
                          "isExistingHmisPatient": true
                        }
                        """),

                ["Patient_SetupProfile"] = new(
                    summary: "Complete first-time profile setup",
                    description: """
                        Completes demographics after registration: CNIC, DOB, gender, blood group, email.
                        Required when `profileSetupRequired` is true after `register`.
                        """,
                    requestExample: """
                        {
                          "mrNo": "010-002-152",
                          "firstName": "Ali",
                          "lastName": "Khan",
                          "cnic": "3520212345671",
                          "dateOfBirth": "1990-05-15T00:00:00",
                          "gender": "M",
                          "bloodGroup": "O+",
                          "email": "ali@example.com"
                        }
                        """,
                    responseExample: """
                        {
                          "success": true,
                          "message": "Profile setup completed",
                          "profileSetupRequired": false,
                          "profile": {
                            "mrNo": "010-002-152",
                            "firstName": "Ali",
                            "lastName": "Khan",
                            "gender": "M",
                            "dateOfBirth": "1990-05-15T00:00:00",
                            "cnic": "3520212345671",
                            "bloodGroup": "O+",
                            "emailAddress": "ali@example.com"
                          }
                        }
                        """),

                ["Messaging_CreateThread"] = new(
                    summary: "Create secure messaging thread",
                    description: "Starts a new secure message thread with hospital staff.",
                    requestExample: """
                        {
                          "mrNo": "010-002-152",
                          "subject": "Question about lab report",
                          "category": "Reports",
                          "initialMessage": "Please review my latest CBC results."
                        }
                        """,
                    responseExample: """
                        {
                          "success": true,
                          "message": "Thread created successfully",
                          "threadId": 42
                        }
                        """),

                ["Messaging_GetInbox"] = new(
                    summary: "List message inbox",
                    description: "Returns all messaging threads for the patient, newest first.",
                    responseExample: """
                        {
                          "success": true,
                          "data": [
                            {
                              "threadId": 42,
                              "mrNo": "010-002-152",
                              "subject": "Question about lab report",
                              "category": "Reports",
                              "status": "OPEN",
                              "lastMessagePreview": "Please review my latest CBC results."
                            }
                          ]
                        }
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["mrNo"] = "Patient MR number from JWT.",
                    }),

                ["Messaging_GetMessages"] = new(
                    summary: "Get chat history for a thread",
                    description: "Paginated message history including attachment metadata.",
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["threadId"] = "Thread ID from inbox.",
                        ["mrNo"] = "Patient MR number.",
                        ["pageNumber"] = "Page number (default 1).",
                        ["pageSize"] = "Page size (default 50, max 100).",
                    }),

                ["Messaging_SendMessage"] = new(
                    summary: "Send text message in thread",
                    description: "Posts a patient message into an existing thread.",
                    requestExample: """
                        {
                          "mrNo": "010-002-152",
                          "body": "Thank you, I will visit OPD tomorrow."
                        }
                        """),

                ["Messaging_UploadAttachment"] = new(
                    summary: "Upload file attachment to thread",
                    description: """
                        Multipart form upload. Allowed: PDF, JPG, PNG, GIF, WEBP (max 10 MB).
                        Form fields: `mrNo`, `file`, optional `body`.
                        """),

                ["Medications_GetCurrentMedications"] = new(
                    summary: "List current medications",
                    description: "Returns distinct active medications from HMIS prescriptions (last 6 months).",
                    responseExample: """
                        {
                          "success": true,
                          "count": 2,
                          "data": [
                            {
                              "medicationId": 12345,
                              "medicineName": "Metformin 500mg",
                              "dosage": "1 tablet",
                              "doseWhen": "After meals",
                              "doctor": "Dr. Ahmed",
                              "visitDate": "2026-08-01T00:00:00"
                            }
                          ]
                        }
                        """,
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["mrNo"] = "Patient MR number.",
                    }),

                ["Medications_GetMedicationDetail"] = new(
                    summary: "Get medication detail",
                    description: "Full prescription line detail by medication ID (PP_ID).",
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["medicationId"] = "Medication ID from current medications list.",
                        ["mrNo"] = "Patient MR number.",
                    }),

                ["Medications_RequestRefill"] = new(
                    summary: "Request medication refill",
                    description: "Submits a refill request to the hospital pharmacy workflow.",
                    requestExample: """
                        {
                          "mrNo": "010-002-152",
                          "medicationId": 12345,
                          "quantity": 30,
                          "notes": "Running low, please approve refill."
                        }
                        """,
                    responseExample: """
                        {
                          "success": true,
                          "message": "Refill request submitted",
                          "data": {
                            "refillId": 7,
                            "status": "PENDING",
                            "medicationName": "Metformin 500mg"
                          }
                        }
                        """),

                ["Medications_GetRefillStatus"] = new(
                    summary: "Get refill request status",
                    description: "Returns status for a single refill request (PENDING, APPROVED, REJECTED, etc.).",
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["refillId"] = "Refill request ID.",
                        ["mrNo"] = "Patient MR number.",
                    }),

                ["Medications_GetRefillHistory"] = new(
                    summary: "List refill requests for patient",
                    description: "Returns all refill requests for the patient, newest first.",
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["mrNo"] = "Patient MR number.",
                    }),

                ["MedicationReminders_GetReminders"] = new(
                    summary: "List medication reminders",
                    description: "Returns all reminder schedules for the patient.",
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["mrNo"] = "Patient MR number.",
                    }),

                ["MedicationReminders_CreateReminder"] = new(
                    summary: "Create medication reminder",
                    description: """
                        Creates a daily reminder. `reminderTime` uses 24h format (e.g. `09:30`).
                        `daysOfWeek`: 1=Mon … 7=Sun (default `1234567` = every day).
                        Server push requires registered FCM device token.
                        """,
                    requestExample: """
                        {
                          "mrNo": "010-002-152",
                          "medicationId": 12345,
                          "medicationName": "Metformin 500mg",
                          "reminderTime": "09:30",
                          "daysOfWeek": "1234567",
                          "isEnabled": true
                        }
                        """),

                ["MedicationReminders_UpdateReminder"] = new(
                    summary: "Update medication reminder",
                    description: "Partial update — only supplied fields are changed."),

                ["MedicationReminders_DeleteReminder"] = new(
                    summary: "Delete medication reminder",
                    description: "Permanently removes a medication reminder schedule.",
                    parameterDescriptions: new Dictionary<string, string>
                    {
                        ["reminderId"] = "Reminder ID to delete.",
                        ["mrNo"] = "Patient MR number.",
                    }),
            };

        public static IReadOnlyDictionary<string, string> TagDescriptions { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Auth"] = "Authentication, OTP, and password reset. Most endpoints are public.",
                ["Doctor"] = "Doctor directory, schedules, and specializations. Public endpoints.",
                ["Patient"] = "Patient profile, health records, appointments, and discharge history. Requires JWT except guest booking and password reset.",
                ["PatientReport"] = "PDF report generation. Requires JWT.",
                ["Billing"] = "Billing overview, invoice details, payment history, and payment summary. Requires JWT.",
                ["PushNotification"] = "FCM push notification register/unregister and hospital-triggered sends. Requires JWT.",
                ["Messaging"] = "Secure patient-to-hospital messaging with file attachments. Requires JWT.",
                ["Medications"] = "Current medications from HMIS prescriptions and refill requests. Requires JWT.",
                ["MedicationReminders"] = "Medication reminder CRUD and server-side push triggers. Requires JWT.",
            };
    }
}
