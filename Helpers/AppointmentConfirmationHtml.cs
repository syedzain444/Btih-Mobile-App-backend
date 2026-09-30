using System.Globalization;
using System.Net;
using System.Text;
using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Helpers
{
    /// <summary>Mobile-first HTML page shown when a confirmation QR is opened in a browser.</summary>
    public static class AppointmentConfirmationHtml
    {
        public static string BuildVerifiedPage(AppointmentConfirmationQrDto qr)
        {
            var patient = Display(qr.PatientName, "Patient");
            var doctor = Display(qr.DoctorName, "Doctor");
            var department = Display(qr.DepartmentHint, "—");
            var when = Display(qr.AppointmentTime, "—");
            var status = Display(qr.Status, "Pending");
            var mrNo = Display(qr.MrNo, "—");
            var phone = Display(qr.Phone, "—");
            var purpose = string.IsNullOrWhiteSpace(qr.Purpose) ||
                          string.Equals(qr.Purpose.Trim(), "NILL", StringComparison.OrdinalIgnoreCase)
                ? "General consultation"
                : qr.Purpose.Trim();
            var appointmentId = Display(qr.AppointmentId, "—");
            var created = qr.CreatedAt == default
                ? "—"
                : qr.CreatedAt.ToString("dd MMM yyyy, hh:mm tt", CultureInfo.InvariantCulture);
            var statusClass = StatusCssClass(status);

            return Wrap(
                title: "Appointment Verified · BTIH",
                body: $@"
<header class=""top"">
  <div class=""brand"">
    <div class=""crest"" aria-hidden=""true"">B</div>
    <div>
      <p class=""brand-name"">BTIH</p>
      <p class=""brand-sub"">Bahria Town International Hospital</p>
    </div>
  </div>
</header>

<main class=""sheet"">
  <div class=""verified"">
    <div class=""check"" aria-hidden=""true"">
      <svg viewBox=""0 0 24 24"" width=""28"" height=""28"" fill=""none"" stroke=""currentColor"" stroke-width=""2.4"" stroke-linecap=""round"" stroke-linejoin=""round"">
        <path d=""M20 6L9 17l-5-5""/>
      </svg>
    </div>
    <p class=""eyebrow"">Check-in QR</p>
    <h1>Appointment verified</h1>
    <p class=""lede"">This confirmation QR is valid. Present this screen at the reception desk.</p>
  </div>

  <section class=""hero"">
    <p class=""label"">Patient</p>
    <h2>{Esc(patient)}</h2>
    <p class=""meta"">MR No. {Esc(mrNo)}</p>
  </section>

  <section class=""grid"">
    <article class=""tile wide"">
      <p class=""label"">Doctor</p>
      <p class=""value"">{Esc(doctor)}</p>
      <p class=""hint"">{Esc(department)}</p>
    </article>
    <article class=""tile"">
      <p class=""label"">When</p>
      <p class=""value"">{Esc(when)}</p>
    </article>
    <article class=""tile"">
      <p class=""label"">Status</p>
      <p class=""value""><span class=""pill {statusClass}"">{Esc(status)}</span></p>
    </article>
    <article class=""tile"">
      <p class=""label"">Appointment ID</p>
      <p class=""value mono"">{Esc(appointmentId)}</p>
    </article>
    <article class=""tile"">
      <p class=""label"">Phone</p>
      <p class=""value"">{Esc(phone)}</p>
    </article>
    <article class=""tile wide"">
      <p class=""label"">Purpose</p>
      <p class=""value"">{Esc(purpose)}</p>
    </article>
  </section>

  <p class=""stamp"">Verified · {Esc(created)}</p>
</main>

<footer class=""foot"">
  <p>For hospital staff use · Do not share this link publicly</p>
</footer>");
        }

        public static string BuildErrorPage(string title, string message)
        {
            return Wrap(
                title: $"{title} · BTIH",
                body: $@"
<header class=""top"">
  <div class=""brand"">
    <div class=""crest"" aria-hidden=""true"">B</div>
    <div>
      <p class=""brand-name"">BTIH</p>
      <p class=""brand-sub"">Bahria Town International Hospital</p>
    </div>
  </div>
</header>
<main class=""sheet error"">
  <div class=""verified warn"">
    <div class=""check"" aria-hidden=""true"">!</div>
    <p class=""eyebrow"">Confirmation QR</p>
    <h1>{Esc(title)}</h1>
    <p class=""lede"">{Esc(message)}</p>
  </div>
</main>
<footer class=""foot"">
  <p>Please ask reception for assistance</p>
</footer>");
        }

        private static string Wrap(string title, string body)
        {
            var sb = new StringBuilder(8192);
            sb.Append("<!DOCTYPE html><html lang=\"en\"><head>");
            sb.Append("<meta charset=\"utf-8\"/>");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1,viewport-fit=cover\"/>");
            sb.Append("<meta name=\"color-scheme\" content=\"light\"/>");
            sb.Append("<meta name=\"theme-color\" content=\"#7D1D2B\"/>");
            sb.Append("<title>").Append(Esc(title)).Append("</title>");
            sb.Append("<style>").Append(Css).Append("</style>");
            sb.Append("</head><body>");
            sb.Append(body);
            sb.Append("</body></html>");
            return sb.ToString();
        }

        private static string Display(string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        private static string Esc(string? value) =>
            WebUtility.HtmlEncode(value ?? string.Empty);

        private static string StatusCssClass(string status)
        {
            var s = status.Trim().ToLowerInvariant();
            if (s.Contains("cancel")) return "pill-cancel";
            if (s.Contains("complete") || s.Contains("confirm") || s.Contains("arriv")) return "pill-ok";
            if (s.Contains("resched")) return "pill-warn";
            return "pill-pending";
        }

        private const string Css = @"
:root{
  --maroon:#A62639;
  --deep:#7D1D2B;
  --dusk:#3B0A18;
  --blush:#FBEEEF;
  --soft:#F6DEE1;
  --ink:#2C2224;
  --muted:#8C7678;
  --line:#E7DFDC;
  --paper:#FFF8F8;
  --ok:#1F6B4A;
  --ok-bg:#E5F3EC;
  --warn:#8A5A12;
  --warn-bg:#F8EED9;
  --bad:#8B1E2D;
  --bad-bg:#F8E4E7;
}
*{box-sizing:border-box}
html,body{margin:0;padding:0;min-height:100%}
body{
  font-family:""Segoe UI"",system-ui,-apple-system,sans-serif;
  color:var(--ink);
  background:
    radial-gradient(1200px 600px at 10% -10%, #F6DEE1 0%, transparent 55%),
    radial-gradient(900px 500px at 100% 0%, #EDD5D9 0%, transparent 50%),
    linear-gradient(180deg,#FFFDFD 0%, #F7F1F2 100%);
  -webkit-font-smoothing:antialiased;
}
.top{padding:22px 20px 8px;max-width:520px;margin:0 auto}
.brand{display:flex;align-items:center;gap:12px}
.crest{
  width:44px;height:44px;border-radius:14px;
  background:linear-gradient(145deg,var(--maroon),var(--deep));
  color:#fff;display:grid;place-items:center;
  font-weight:700;font-size:1.15rem;letter-spacing:-.02em;
  box-shadow:0 10px 24px rgba(125,29,43,.28);
}
.brand-name{margin:0;font-size:1.05rem;font-weight:700;letter-spacing:.08em;color:var(--dusk)}
.brand-sub{margin:2px 0 0;font-size:.78rem;color:var(--muted)}
.sheet{
  max-width:520px;margin:10px auto 28px;padding:0 16px 8px;
}
.verified{
  background:linear-gradient(160deg,var(--deep) 0%, var(--maroon) 55%, #C24957 100%);
  color:#fff;border-radius:24px;padding:28px 22px 24px;
  box-shadow:0 18px 40px rgba(125,29,43,.28);
  position:relative;overflow:hidden;
}
.verified::after{
  content:"""";position:absolute;inset:auto -40px -60px auto;width:180px;height:180px;
  background:rgba(255,255,255,.08);border-radius:50%;
}
.verified.warn{background:linear-gradient(160deg,#5C2A33,#8B3A48)}
.check{
  width:48px;height:48px;border-radius:16px;background:rgba(255,255,255,.16);
  display:grid;place-items:center;margin-bottom:14px;backdrop-filter:blur(6px);
  font-size:1.4rem;font-weight:700;
}
.eyebrow{margin:0;font-size:.72rem;letter-spacing:.14em;text-transform:uppercase;opacity:.82}
.verified h1{margin:8px 0 8px;font-size:1.65rem;line-height:1.15;font-weight:700;letter-spacing:-.02em}
.lede{margin:0;font-size:.95rem;line-height:1.45;opacity:.92;max-width:34ch}
.hero{
  margin:18px 0 14px;padding:18px 18px 16px;background:var(--paper);
  border:1px solid var(--line);border-radius:18px;
}
.hero h2{margin:4px 0 6px;font-size:1.35rem;letter-spacing:-.02em}
.meta{margin:0;color:var(--muted);font-size:.9rem}
.label{margin:0 0 6px;font-size:.7rem;letter-spacing:.12em;text-transform:uppercase;color:var(--muted);font-weight:600}
.grid{display:grid;grid-template-columns:1fr 1fr;gap:10px}
.tile{
  background:#fff;border:1px solid var(--line);border-radius:16px;padding:14px 14px 13px;
}
.tile.wide{grid-column:1 / -1}
.value{margin:0;font-size:1rem;font-weight:600;line-height:1.35;color:var(--ink);word-break:break-word}
.hint{margin:6px 0 0;font-size:.85rem;color:var(--muted);line-height:1.35}
.mono{font-variant-numeric:tabular-nums;letter-spacing:.02em}
.pill{
  display:inline-block;padding:5px 10px;border-radius:999px;font-size:.82rem;font-weight:700;
}
.pill-pending{background:var(--soft);color:var(--deep)}
.pill-ok{background:var(--ok-bg);color:var(--ok)}
.pill-warn{background:var(--warn-bg);color:var(--warn)}
.pill-cancel{background:var(--bad-bg);color:var(--bad)}
.stamp{margin:16px 4px 0;text-align:center;font-size:.78rem;color:var(--muted)}
.foot{max-width:520px;margin:0 auto;padding:0 20px 36px;text-align:center;color:var(--muted);font-size:.75rem}
@media (max-width:380px){
  .grid{grid-template-columns:1fr}
  .tile.wide{grid-column:auto}
}
";
    }
}
