using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;
using BocconiLMS.Data;

namespace BocconiLMS.Services;

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Bocconi LMS";
    public bool UseSsl { get; set; } = false;
    public bool Enabled { get; set; } = false;
}

public class EmailService
{
    private readonly SmtpSettings _defaults;
    private readonly SettingsRepository _settingsRepo;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<SmtpSettings> defaults,
        SettingsRepository settingsRepo,
        ILogger<EmailService> logger)
    {
        _defaults = defaults.Value;
        _settingsRepo = settingsRepo;
        _logger = logger;
    }

    public async Task<SmtpSettings> GetEffectiveSettingsAsync()
    {
        var db = await _settingsRepo.GetByPrefixAsync("Smtp:");

        string Get(string key, string fallback) =>
            db.TryGetValue("Smtp:" + key, out var v) && !string.IsNullOrEmpty(v) ? v! : fallback;

        bool GetBool(string key, bool fallback) =>
            db.TryGetValue("Smtp:" + key, out var v) && !string.IsNullOrEmpty(v)
                ? v!.Equals("true", StringComparison.OrdinalIgnoreCase)
                : fallback;

        int GetInt(string key, int fallback) =>
            db.TryGetValue("Smtp:" + key, out var v) && int.TryParse(v, out var n) ? n : fallback;

        return new SmtpSettings
        {
            Enabled  = GetBool("Enabled",   _defaults.Enabled),
            Host     = Get("Host",           _defaults.Host),
            Port     = GetInt("Port",        _defaults.Port),
            Username = Get("Username",       _defaults.Username),
            Password = Get("Password",       _defaults.Password),
            FromEmail= Get("FromEmail",      _defaults.FromEmail),
            FromName = Get("FromName",       _defaults.FromName),
            UseSsl   = GetBool("UseSsl",     _defaults.UseSsl),
        };
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string toName, string courseTitle, string teacherName)
    {
        var subject = $"Benvenuto nel corso: {courseTitle}";
        var body = $@"
<html><body style='font-family: Arial, sans-serif; color: #333;'>
<div style='max-width:600px; margin:0 auto; padding:24px;'>
  <h2 style='color:#003366;'>Iscrizione confermata</h2>
  <p>Ciao <strong>{HtmlEncode(toName)}</strong>,</p>
  <p>La tua iscrizione al corso <strong>{HtmlEncode(courseTitle)}</strong> è stata completata con successo.</p>
  <p>Il corso è tenuto da <strong>{HtmlEncode(teacherName)}</strong>.</p>
  <p>Accedi alla piattaforma per iniziare a seguire le lezioni.</p>
  <hr style='border:none;border-top:1px solid #ddd;margin:24px 0;'/>
  <p style='color:#888;font-size:12px;'>Bocconi LMS – notifica automatica</p>
</div>
</body></html>";
        await SendAsync(toEmail, toName, subject, body);
    }

    public async Task SendQuizResultToTeacherAsync(
        string teacherEmail, string teacherName,
        string studentName, string studentEmail,
        string quizTitle, string courseTitle,
        int score, bool passed)
    {
        var result = passed ? "SUPERATO ✓" : "NON SUPERATO ✗";
        var color = passed ? "#2e7d32" : "#c62828";
        var subject = $"Risultato quiz – {studentName} – {quizTitle}";
        var body = $@"
<html><body style='font-family: Arial, sans-serif; color: #333;'>
<div style='max-width:600px; margin:0 auto; padding:24px;'>
  <h2 style='color:#003366;'>Notifica risultato quiz</h2>
  <p>Ciao <strong>{HtmlEncode(teacherName)}</strong>,</p>
  <p>Lo studente <strong>{HtmlEncode(studentName)}</strong> ({HtmlEncode(studentEmail)}) ha completato il quiz <strong>{HtmlEncode(quizTitle)}</strong> nel corso <strong>{HtmlEncode(courseTitle)}</strong>.</p>
  <table style='border-collapse:collapse;margin:16px 0;'>
    <tr>
      <td style='padding:8px 16px 8px 0;color:#555;'>Punteggio:</td>
      <td style='padding:8px 0;'><strong>{score}%</strong></td>
    </tr>
    <tr>
      <td style='padding:8px 16px 8px 0;color:#555;'>Esito:</td>
      <td style='padding:8px 0;'><strong style='color:{color};'>{result}</strong></td>
    </tr>
  </table>
  <hr style='border:none;border-top:1px solid #ddd;margin:24px 0;'/>
  <p style='color:#888;font-size:12px;'>Bocconi LMS – notifica automatica</p>
</div>
</body></html>";
        await SendAsync(teacherEmail, teacherName, subject, body);
    }

    public async Task SendNewLessonNotificationAsync(string toEmail, string toName, string lessonTitle, string courseTitle)
    {
        var subject = $"Nuova lezione disponibile in \"{courseTitle}\"";
        var body = $@"
<html><body style='font-family: Arial, sans-serif; color: #333;'>
<div style='max-width:600px; margin:0 auto; padding:24px;'>
  <h2 style='color:#003366;'>Nuova lezione disponibile</h2>
  <p>Ciao <strong>{HtmlEncode(toName)}</strong>,</p>
  <p>È stata pubblicata una nuova lezione nel corso <strong>{HtmlEncode(courseTitle)}</strong> a cui sei iscritto/a:</p>
  <div style='background:#f5f7fa;border-left:4px solid #003366;padding:12px 16px;margin:16px 0;border-radius:4px;'>
    <strong style='font-size:16px;'>{HtmlEncode(lessonTitle)}</strong>
  </div>
  <p>Accedi alla piattaforma per visualizzare la lezione e continuare il tuo percorso formativo.</p>
  <hr style='border:none;border-top:1px solid #ddd;margin:24px 0;'/>
  <p style='color:#888;font-size:12px;'>Bocconi LMS – notifica automatica</p>
</div>
</body></html>";
        await SendAsync(toEmail, toName, subject, body);
    }

    public async Task SendLessonReminderAsync(string toEmail, string toName, string courseTitle, int incompleteLessons)
    {
        var subject = $"Promemoria: hai lezioni da completare in \"{courseTitle}\"";
        var lessonWord = incompleteLessons == 1 ? "lezione" : "lezioni";
        var body = $@"
<html><body style='font-family: Arial, sans-serif; color: #333;'>
<div style='max-width:600px; margin:0 auto; padding:24px;'>
  <h2 style='color:#003366;'>Promemoria lezioni</h2>
  <p>Ciao <strong>{HtmlEncode(toName)}</strong>,</p>
  <p>Hai ancora <strong>{incompleteLessons} {lessonWord}</strong> da completare nel corso <strong>{HtmlEncode(courseTitle)}</strong>.</p>
  <p>Accedi alla piattaforma per continuare il tuo percorso formativo.</p>
  <hr style='border:none;border-top:1px solid #ddd;margin:24px 0;'/>
  <p style='color:#888;font-size:12px;'>Bocconi LMS – notifica automatica. Rispondi a questa email per disiscriverti dai promemoria.</p>
</div>
</body></html>";
        await SendAsync(toEmail, toName, subject, body);
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink)
    {
        var subject = "Bocconi LMS – Reimposta la tua password";
        var body = $@"
<html><body style='font-family: Arial, sans-serif; color: #333;'>
<div style='max-width:600px; margin:0 auto; padding:24px;'>
  <h2 style='color:#003366;'>Reimposta la tua password</h2>
  <p>Ciao <strong>{HtmlEncode(toName)}</strong>,</p>
  <p>Abbiamo ricevuto una richiesta di reimpostazione della password per il tuo account.</p>
  <p>Clicca sul pulsante qui sotto per scegliere una nuova password. Il link è valido per <strong>1 ora</strong>.</p>
  <div style='margin:24px 0;text-align:center;'>
    <a href='{resetLink}' style='background:#003366;color:#fff;padding:12px 28px;border-radius:6px;text-decoration:none;font-weight:bold;display:inline-block;'>Reimposta password</a>
  </div>
  <p style='color:#666;font-size:13px;'>Se non hai richiesto il reset della password, ignora questa email. Il link scadrà automaticamente.</p>
  <hr style='border:none;border-top:1px solid #ddd;margin:24px 0;'/>
  <p style='color:#888;font-size:12px;'>Bocconi LMS – notifica automatica</p>
</div>
</body></html>";
        await SendAsync(toEmail, toName, subject, body);
    }

    public async Task SendTestEmailAsync(string toEmail, SmtpSettings? overrideSettings = null)
    {
        var settings = overrideSettings ?? await GetEffectiveSettingsAsync();
        var subject = "Bocconi LMS – Email di test";
        var body = $@"
<html><body style='font-family: Arial, sans-serif; color: #333;'>
<div style='max-width:600px; margin:0 auto; padding:24px;'>
  <h2 style='color:#003366;'>Email di test</h2>
  <p>Questa è un'email di prova inviata dalla piattaforma <strong>Bocconi LMS</strong>.</p>
  <p>La configurazione SMTP funziona correttamente.</p>
  <p><strong>Host:</strong> {HtmlEncode(settings.Host)}:{settings.Port}<br/>
     <strong>From:</strong> {HtmlEncode(settings.FromName)} &lt;{HtmlEncode(settings.FromEmail)}&gt;</p>
  <hr style='border:none;border-top:1px solid #ddd;margin:24px 0;'/>
  <p style='color:#888;font-size:12px;'>Bocconi LMS – test invio email</p>
</div>
</body></html>";
        await SendWithSettingsAsync(settings, toEmail, toEmail, subject, body, skipEnabledCheck: true);
    }

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var settings = await GetEffectiveSettingsAsync();
        await SendWithSettingsAsync(settings, toEmail, toName, subject, htmlBody);
    }

    private async Task SendWithSettingsAsync(
        SmtpSettings settings,
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        bool skipEnabledCheck = false)
    {
        if (!skipEnabledCheck && !settings.Enabled)
        {
            _logger.LogInformation("Email not sent (SMTP disabled). To: {Email}, Subject: {Subject}", toEmail, subject);
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            _logger.LogWarning("SMTP host not configured. Skipping email to {Email}", toEmail);
            if (skipEnabledCheck)
                throw new InvalidOperationException("Host SMTP non configurato.");
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            var secureOption = settings.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTlsWhenAvailable;

            await client.ConnectAsync(settings.Host, settings.Port, secureOption);

            if (!string.IsNullOrWhiteSpace(settings.Username))
                await client.AuthenticateAsync(settings.Username, settings.Password);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}: {Subject}", toEmail, subject);
            throw;
        }
    }

    private static string HtmlEncode(string text) =>
        System.Net.WebUtility.HtmlEncode(text);
}
