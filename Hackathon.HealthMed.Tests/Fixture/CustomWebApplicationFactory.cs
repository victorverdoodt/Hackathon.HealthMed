using System.Linq;
using System.Threading.Tasks;
using Hackathon.HealthMed.API;
using Hackathon.HealthMed.Domain.Models.Entities;
using Hackathon.HealthMed.Domain.Models.Enum;
using Hackathon.HealthMed.Domain.Models.Interfaces;
using Hackathon.HealthMed.Infrastrucuture.Databases;
using Hackathon.HealthMed.Infrastrucuture.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Hackathon.HealthMed.Tests.Fixture
{
    [CollectionDefinition("Postgres Integration Collection")]
    public class PostgresIntegrationCollection : ICollectionFixture<CustomWebApplicationFactory> { }

    public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        // Container do PostgreSQL
        private readonly PostgreSqlContainer _postgreSqlContainer = new PostgreSqlBuilder()
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithImage("postgres:14-alpine")
            .WithCleanUp(true)
            .Build();

        // Container do Redis
        private readonly RedisContainer _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithCleanUp(true)
            .Build();

        // Inicia os containers antes dos testes
        public async Task InitializeAsync()
        {
            await _postgreSqlContainer.StartAsync();
            await _redisContainer.StartAsync();
        }

        // Finaliza e libera os containers após os testes
        public new async Task DisposeAsync()
        {
            await _postgreSqlContainer.DisposeAsync().AsTask();
            await _redisContainer.DisposeAsync().AsTask();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remover registro existente do SchedulingContext (se houver).
                var dbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<SchedulingContext>));
                if (dbContextDescriptor != null)
                    services.Remove(dbContextDescriptor);

                // Registra o SchedulingContext utilizando a string de conexão do container PostgreSQL.
                services.AddDbContext<SchedulingContext>(options =>
                {
                    options.UseNpgsql(_postgreSqlContainer.GetConnectionString());
                });


                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = _redisContainer.GetConnectionString();
                    options.InstanceName = "ApiInstance";
                });

                // Constrói o provedor de serviços e executa as migrações/seed de dados.
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SchedulingContext>();
                db.Database.Migrate();

                // Seed de dados (exemplo: médicos, pacientes, regras de agenda)
                if (!db.Doctors.Any())
                {
                    // Exemplo de seed para médicos
                    PasswordHasher.CreatePasswordHash("password", out string doctorHash, out string doctorSalt);
                    db.Doctors.AddRange(
                        new Doctor
                        {
                            Name = "Dr. John Doe",
                            LicenseNumber = "12345",
                            Specialty = Specialty.Cardiology,
                            ConsultationFee = 200,
                            Latitude = -23.55,
                            Longitude = -46.63,
                            Rating = 4.5,
                            PasswordHash = doctorHash,
                            PasswordSalt = doctorSalt
                        },
                        new Doctor
                        {
                            Name = "Dr. Jane Smith",
                            LicenseNumber = "67890",
                            Specialty = Specialty.Neurology,
                            ConsultationFee = 250,
                            Latitude = -23.56,
                            Longitude = -46.64,
                            Rating = 4.7,
                            PasswordHash = doctorHash, // Para o exemplo, usamos o mesmo hash
                            PasswordSalt = doctorSalt
                        }
                    );
                }

                db.SaveChanges();

                if (!db.Patients.Any())
                {
                    PasswordHasher.CreatePasswordHash("password", out string patientHash, out string patientSalt);
                    db.Patients.AddRange(
                        new Patient
                        {
                            Name = "Patient One",
                            Email = "patient1@example.com",
                            NationalId = "11111111111",
                            PasswordHash = patientHash,
                            PasswordSalt = patientSalt
                        },
                        new Patient
                        {
                            Name = "Patient Two",
                            Email = "patient2@example.com",
                            NationalId = "22222222222",
                            PasswordHash = patientHash,
                            PasswordSalt = patientSalt
                        }
                    );
                }

                db.SaveChanges();

                if (!db.DoctorScheduleRules.Any())
                {
                    var doctor = db.Doctors.FirstOrDefault(d => d.LicenseNumber == "12345");
                    if (doctor != null)
                    {
                        db.DoctorScheduleRules.Add(new DoctorScheduleRule
                        {
                            DoctorId = doctor.Id,
                            ScheduleType = ScheduleType.Available,
                            FrequencyType = FrequencyType.Weekly,
                            StartDate = System.DateTime.UtcNow.Date,
                            EndDate = System.DateTime.UtcNow.Date.AddMonths(1),
                            DaysOfWeek = "Mon,Wed,Fri",
                            StartTimeOfDay = new System.TimeSpan(8, 0, 0),
                            EndTimeOfDay = new System.TimeSpan(12, 0, 0)
                        });
                    }
                }

                db.SaveChanges();
            });
        }
    }
}
