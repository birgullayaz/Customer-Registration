using Microsoft.AspNetCore.Mvc;
using Islemler.Components.Pages;
using Islemler.Models;
using Hangfire;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using System;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace ISLEMLER.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class HomeController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IConfiguration configuration, ILogger<HomeController> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // Her gün saat 09:00'da çalışacak şekilde ayarla
            RecurringJob.AddOrUpdate(
                "daily-notification-job",
                () => SendDailyNotification(),
                "0 9 * * *" // Cron expression: Her gün saat 09:00'da
            );
        }

        /// <summary>
        /// Ana sayfayı görüntüler
        /// </summary>
        /// <returns>HTML içerikli ana sayfa</returns>
        /// <response code="200">Başarılı</response>
        /// <response code="500">Sunucu hatası</response>
        [HttpGet]
        [Route("index")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult Index()
        {
            try
            {
                // HTML içeriğini doğrudan dön
                var html = @"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <title>Customer Registration System</title>
                        <meta charset='utf-8' />
                        <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css' rel='stylesheet'>
                    </head>
                    <body>
                        <div class='container mt-5'>
                            <h1>Customer Registration System</h1>
                            <div class='row mt-4'>
                                <div class='col-md-6'>
                                    <div class='card'>
                                        <div class='card-body'>
                                            <h5 class='card-title'>API Documentation</h5>
                                            <p class='card-text'>View API documentation using Swagger</p>
                                            <a href='/swagger' class='btn btn-primary'>Go to Swagger</a>
                                        </div>
                                    </div>
                                </div>
                                <div class='col-md-6'>
                                    <div class='card'>
                                        <div class='card-body'>
                                            <h5 class='card-title'>Job Dashboard</h5>
                                            <p class='card-text'>Monitor background jobs</p>
                                            <a href='/hangfire' class='btn btn-secondary'>Go to Hangfire</a>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </body>
                    </html>";

                return Content(html, "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Index error: {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Yeni kullanıcı kaydı oluşturur
        /// </summary>
        /// <param name="username">Kullanıcı adı</param>
        /// <param name="email">Email adresi</param>
        /// <returns>Kayıt sonucu ve kullanıcı bilgileri</returns>
        /// <response code="200">Kayıt başarılı</response>
        /// <response code="400">Geçersiz veri</response>
        /// <response code="500">Sunucu hatası</response>
        [HttpPost]
        [Route("send-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SendDataToDB([FromForm] string username, [FromForm] string email)
        {
            try
            {
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email))
                {
                    return BadRequest("Username and email are required");
                }

                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = connection;
                        cmd.CommandText = "INSERT INTO \"SecondUsers\" (name, email) VALUES (@username, @email) RETURNING id";
                        cmd.Parameters.AddWithValue("username", username);
                        cmd.Parameters.AddWithValue("email", email);
                        var result = await cmd.ExecuteScalarAsync();
                        var userId = result != null ? (int)result : throw new InvalidOperationException("Failed to get user ID");

                        // Email işlemleri
                       // BackgroundJob.Enqueue(() => SendWelcomeEmail(email));
                       // BackgroundJob.Schedule(() => SendReminderEmail(email), TimeSpan.FromHours(1));
                        
                        // Başarılı kayıt sonrası email ve userId dön
                        return Ok(new { 
                            message = "User registered successfully", 
                            userId = userId,
                            email = email 
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"SendDataToDB error: {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [NonAction]
        public async Task SendWelcomeEmail(string username)
        {
            await SendEmail(
                username,
                "Hoş Geldiniz!",
                $"Sayın {username}, kaydınız başarıyla oluşturuldu. Sitemize hoş geldiniz!"
            );
        }

        [NonAction]
        public async Task SendReminderEmail(string username)
        {
            await SendEmail(
                username,
                "Profil Hatırlatması",
                $"Sayın {username}, profilinizi güncellemeyi unutmayın!"
            );
        }

        [NonAction]
        public async Task SendDailyNotification()
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var cmd = new NpgsqlCommand("SELECT name, email FROM \"SecondUsers\"", connection))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var username = reader["name"]?.ToString();
                                var email = reader["email"]?.ToString();
                                if (!string.IsNullOrEmpty(email))
                                {
                                    await SendEmail(
                                        email,
                                        "Günlük Bilgilendirme",
                                        $"Sayın {username}, bugün için yeni duyurularımız var!"
                                    );
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Daily notification error: {ex.Message}");
                throw;
            }
        }

        [NonAction]
        public async Task SendEmail(string to, string subject, string body)
        {
            try
            {
                if (string.IsNullOrEmpty(to))
                    throw new ArgumentException("Email recipient cannot be empty");

                var smtpSettings = _configuration.GetSection("EmailSettings");
                using (var client = new SmtpClient())
                {
                    client.Host = smtpSettings["SmtpServer"] ?? throw new InvalidOperationException("SMTP Server not configured");
                    client.Port = int.Parse(smtpSettings["SmtpPort"] ?? "587");
                    client.EnableSsl = true;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new System.Net.NetworkCredential(
                        smtpSettings["SmtpUsername"], 
                        smtpSettings["SmtpPassword"]);

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(smtpSettings["SmtpUsername"] ?? throw new InvalidOperationException("SMTP Username not configured")),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true
                    };
                    mailMessage.To.Add(to);

                    await client.SendMailAsync(mailMessage);
                    _logger.LogInformation($"Email sent successfully to {to}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Email sending error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Tüm kullanıcıları listeler
        /// </summary>
        /// <returns>Kullanıcı listesi</returns>
        /// <response code="200">Başarılı</response>
        /// <response code="500">Sunucu hatası</response>
        [HttpGet]
        [Route("get-all")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllData()
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("Database connection string not configured");

                var users = new List<SecondUsers>();

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    
                    using (var cmd = new NpgsqlCommand())
                    {
                        cmd.Connection = connection;
                        cmd.CommandText = "SELECT id, name, email, age FROM \"SecondUsers\"";
                        
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                users.Add(new SecondUsers
                                {
                                    Name = reader["name"]?.ToString() ?? string.Empty,
                                    Email = reader["email"]?.ToString() ?? string.Empty,
                                    Age = reader["age"] != DBNull.Value ? Convert.ToInt32(reader["age"]) : 0
                                });
                            }
                        }
                    }
                }

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetAllData error: {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private void InitializeBackgroundJobs()
        {
            // Anlık email gönderme testi
            BackgroundJob.Enqueue(() => SendWelcomeEmail("test@example.com"));

            // Zamanlanmış email testi (1 dakika sonra)
            BackgroundJob.Schedule(
                () => SendReminderEmail("test@example.com"),
                TimeSpan.FromMinutes(1)
            );

            // Tekrarlayan iş testi (her gün 09:00'da)
            RecurringJob.AddOrUpdate(
                "daily-emails",
                () => SendDailyNotification(),
                "0 9 * * *"
            );
        }
    }
}
