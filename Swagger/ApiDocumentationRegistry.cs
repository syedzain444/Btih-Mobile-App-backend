namespace HospitalMobileAPPApi.Swagger
{
    internal static class ApiDocumentationRegistry
    {
        public static IReadOnlyDictionary<string, ApiDocEntry> Operations { get; } =
            new Dictionary<string, ApiDocEntry>(StringComparer.Ordinal)
            {
                ["Auth_Login"] = new(
                    summary: "Patient login",
                    description: """
                        Authenticates a patient using registered contact number and password.
                        Returns a JWT Bearer token used for all protected endpoints.

                        **Flow:** Call this first → copy `token` → click **Authorize** in Swagger → enter `Bearer {token}`.
                        """,
                    requestExample: """
                        {
                          "contactNo": "03001234567",
                          "password": "yourPassword"
                        }
                        """,
                    responseExample: """
                        {
                          "success": true,
                          "message": "Login successful",
                          "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
                          "tokenType": "Bearer",
                          "expiresAt": "2026-03-08T18:30:00Z",
                          "expiresInSeconds": 5700,
                          "mrNo": "010-002-152",
                          "firstName": "Ali"
                        }
                        """),

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

                ["PatientReport_GetBillingHistory"] = new(
                    summary: "Get billing history",
                    description: "Returns bill/payment history for a patient using Oracle stored procedure `SP_BILL_PAY`.",
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
            };

        public static IReadOnlyDictionary<string, string> TagDescriptions { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Auth"] = "Authentication, OTP, and password reset. Most endpoints are public.",
                ["Doctor"] = "Doctor directory, schedules, and specializations. Public endpoints.",
                ["Patient"] = "Patient profile, health records, appointments, and discharge history. Requires JWT except guest booking and password reset.",
                ["PatientReport"] = "PDF report generation and billing history. Requires JWT.",
                ["PushNotification"] = "FCM push notification register/unregister and hospital-triggered sends. Requires JWT.",
            };
    }
}
