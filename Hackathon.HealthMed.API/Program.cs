
using Hackathon.HealthMed.Application.Services;
using Hackathon.HealthMed.Domain.Models.Interfaces;
using Hackathon.HealthMed.Infrastrucuture.Databases;
using Hackathon.HealthMed.Infrastrucuture.Filters;
using Hackathon.HealthMed.Infrastrucuture.Repositories;
using Hackathon.HealthMed.Infrastrucuture.Services;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Hackathon.HealthMed.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure services.
            builder.Services.AddControllers(options =>
            {
                // Registra o NotificationFilter globalmente para interceptar notificações.
                options.Filters.Add<NotificationFilter>();
            });
            builder.Services.AddDbContext<SchedulingContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection") ??
                                  "Host=localhost;Port=5432;Database=optimizedsched;Username=postgres;Password=postgres"));
            builder.Services.AddScoped<SchedulingService>();
            builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
            builder.Services.AddScoped<ICacheService, CacheService>();
            builder.Services.AddScoped<NotificationContextService>();
            builder.Services.AddHangfire(config => config.UseMemoryStorage());
            builder.Services.AddHangfireServer();
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = builder.Configuration.GetConnectionString("RedisConnection");
                options.InstanceName = "ApiInstance";
            });
            var secretKey = "mysupersecretkey!1234567890123456";
            var issuer = "OptimizedScheduling";
            var audience = "OptimizedSchedulingAudience";
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
                };
            });
            builder.Services.AddAuthorization();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddLocalization();

            var app = builder.Build();

            var supportedCultures = new[] { "en-US", "pt-BR", "es-ES" };
            var localizationOptions = new RequestLocalizationOptions()
                .SetDefaultCulture("pt-BR")
                .AddSupportedCultures(supportedCultures)
                .AddSupportedUICultures(supportedCultures);

            localizationOptions.ApplyCurrentCultureToResponseHeaders = true;
            app.UseRequestLocalization(localizationOptions);
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.UseHangfireDashboard();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            RecurringJob.AddOrUpdate<SchedulingService>(
              "ConsolidateDoctorStatistics",
              service => service.ConsolidateDoctorStatisticsAsync(),
              Cron.Daily);

            app.Run();
        }
    }
}
