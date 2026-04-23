using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

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
    private readonly SmtpSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<SmtpSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
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

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Email not sent (SMTP disabled). To: {Email}, Subject: {Subject}", toEmail, subject);
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.Host))
        {
            _logger.LogWarning("SMTP host not configured. Skipping email to {Email}", toEmail);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            var secureOption = _settings.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTlsWhenAvailable;

            await client.ConnectAsync(_settings.Host, _settings.Port, secureOption);

            if (!string.IsNullOrWhiteSpace(_settings.Username))
                await client.AuthenticateAsync(_settings.Username, _settings.Password);

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
