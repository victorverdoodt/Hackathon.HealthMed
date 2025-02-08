using Hackathon.HealthMed.Application.DTO.Models.Response;
using Hackathon.HealthMed.Application.Services;
using Hackathon.HealthMed.Domain.Models.Entities;
using Hackathon.HealthMed.Domain.Models.Enum;
using Hackathon.HealthMed.Infrastrucuture.Databases;
using Hackathon.HealthMed.Infrastrucuture.Repositories;
using Hackathon.HealthMed.Infrastrucuture.Services;
using Hackathon.HealthMed.Tests.Fixture;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
using System.Data.Common;

namespace Hackathon.HealthMed.Tests.Unit
{
    [Collection("Postgres Unit Collection")]
    [Trait("Category", "Unit")]
    public class SchedulingServiceTests
    {
        private readonly PostgresTestContainerFixture _fixture;

        public SchedulingServiceTests(PostgresTestContainerFixture fixture)
        {
            _fixture = fixture;
        }

        // Cria um SchedulingContext utilizando a string de conexão do container PostgreSQL.
        private SchedulingContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<SchedulingContext>()
                .UseNpgsql(_fixture.GetConnectionString())
                .Options;
            return new SchedulingContext(options);
        }

        [Fact]
        public async Task ConnectionStateReturnsOpen()
        {
            using DbConnection connection = new NpgsqlConnection(_fixture.GetConnectionString());
            connection.Open();
            Assert.Equal(ConnectionState.Open, connection.State);
        }

        [Fact]
        public async Task ScheduleAppointment_ShouldReturnAppointment_WhenSlotIsAvailable()
        {
            using var context = CreateContext();
            await context.Database.MigrateAsync();

            var notificationService = new NotificationContextService();
            var appointmentRepository = new AppointmentRepository(context);
            var cacheService = new FakeCacheService();
            var service = new SchedulingService(context, appointmentRepository, notificationService, cacheService);

            // Cria um médico e um paciente.
            var doctor = new Doctor
            {
                Name = "Dr. Test",
                LicenseNumber = "0001",
                Specialty = Specialty.Cardiology,
                Latitude = 0,
                Longitude = 0,
                ConsultationFee = 100,
                Rating = 5,
                ScheduleRules = new List<DoctorScheduleRule>()
            };
            context.Doctors.Add(doctor);

            var patient = new Patient
            {
                Name = "Test Patient",
                Email = "testpatient@example.com",
                NationalId = "00000000000",
                PasswordHash = "dummyHash",
                PasswordSalt = "dummySalt"
            };
            context.Patients.Add(patient);
            await context.SaveChangesAsync();

            DateTime today = DateTime.UtcNow.Date;
            // Cria uma regra de disponibilidade que cobre o horário desejado.
            await service.CreateScheduleRuleAsync(
                doctor.Id,
                ScheduleType.Available,
                FrequencyType.Daily,
                today,
                today.AddDays(7),
                TimeSpan.FromHours(8),
                TimeSpan.FromHours(17),
                ""
            );

            DateTime appointmentTime = today.AddDays(1).AddHours(9);
            var appointment = await service.ScheduleAppointmentAsync(doctor.Id, patient.Id, appointmentTime, "unique1");

            Assert.NotNull(appointment);
            Assert.Equal(doctor.Id, appointment.DoctorId);
            Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        }

        [Fact]
        public async Task ScheduleAppointment_ShouldAddNotification_WhenNoAvailability()
        {
            using var context = CreateContext();
            await context.Database.MigrateAsync();

            var notificationService = new NotificationContextService();
            var appointmentRepository = new AppointmentRepository(context);
            var cacheService = new FakeCacheService();
            var service = new SchedulingService(context, appointmentRepository, notificationService, cacheService);

            // Cria um médico sem regras de disponibilidade.
            var doctor = new Doctor
            {
                Name = "Dr. NoSchedule",
                LicenseNumber = "0002",
                Specialty = Specialty.Neurology,
                Latitude = 0,
                Longitude = 0,
                ConsultationFee = 100,
                Rating = 5,
            };
            context.Doctors.Add(doctor);

            // Cria o paciente
            var patient = new Patient
            {
                Name = "Test Patient",
                Email = "testpatient@example.com",
                NationalId = "00000000000",
                PasswordHash = "dummyHash",
                PasswordSalt = "dummySalt"
            };
            context.Patients.Add(patient);

            await context.SaveChangesAsync();

            DateTime appointmentTime = DateTime.UtcNow.Date.AddDays(1).AddHours(9);
            var appointment = await service.ScheduleAppointmentAsync(doctor.Id, patient.Id, appointmentTime, "unique2");

            Assert.Null(appointment);
            Assert.True(notificationService.HasNotifications);
        }

        [Fact]
        public async Task CreateScheduleRule_ShouldCreateRuleSuccessfully()
        {
            using var context = CreateContext();
            await context.Database.MigrateAsync();
            var notificationService = new NotificationContextService();
            var cacheService = new FakeCacheService();
            var service = new SchedulingService(context, new AppointmentRepository(context), notificationService, cacheService);

            var doctor = new Doctor
            {
                Name = "Dr. CreateRule",
                LicenseNumber = "CR001",
                Specialty = Specialty.Cardiology,
                ConsultationFee = 150,
                Rating = 0,
                ScheduleRules = new List<DoctorScheduleRule>()
            };
            context.Doctors.Add(doctor);
            await context.SaveChangesAsync();

            DateTime today = DateTime.UtcNow.Date;
            await service.CreateScheduleRuleAsync(
                doctor.Id,
                ScheduleType.Available,
                FrequencyType.Daily,
                today,
                today.AddDays(7),
                TimeSpan.FromHours(9),
                TimeSpan.FromHours(17),
                ""
            );

            var rules = await context.DoctorScheduleRules.Where(r => r.DoctorId == doctor.Id).ToListAsync();
            Assert.Single(rules);
            Assert.Empty(notificationService.Notifications);
        }

        [Fact]
        public async Task CreateScheduleRule_ShouldAddNotification_WhenEndTimeBeforeStartTime()
        {
            using var context = CreateContext();
            await context.Database.MigrateAsync();
            var notificationService = new NotificationContextService();
            var cacheService = new FakeCacheService(); // Implementação fake do ICacheService
            var service = new SchedulingService(context, new AppointmentRepository(context), notificationService, cacheService);

            var doctor = new Doctor
            {
                Name = "Dr. InvalidRule",
                LicenseNumber = "IR001",
                Specialty = Specialty.Neurology,
                ConsultationFee = 200,
                Rating = 0,
                ScheduleRules = new List<DoctorScheduleRule>()
            };
            context.Doctors.Add(doctor);
            await context.SaveChangesAsync();

            DateTime today = DateTime.UtcNow.Date;
            await service.CreateScheduleRuleAsync(
                doctor.Id,
                ScheduleType.Available,
                FrequencyType.Daily,
                today,
                today.AddDays(7),
                TimeSpan.FromHours(17),  // Start at 17:00
                TimeSpan.FromHours(9),   // End at 09:00 (invalid)
                ""
            );

            Assert.True(notificationService.HasNotifications);
            Assert.Contains(notificationService.Notifications, n => n.Key == "InvalidTime");
        }

        [Fact]
        public async Task UpdateScheduleRule_ShouldUpdateRuleSuccessfully()
        {
            using var context = CreateContext();
            await context.Database.MigrateAsync();
            var notificationService = new NotificationContextService();
            var cacheService = new FakeCacheService();
            var service = new SchedulingService(context, new AppointmentRepository(context), notificationService, cacheService);

            var doctor = new Doctor
            {
                Name = "Dr. Update",
                LicenseNumber = "UP001",
                Specialty = Specialty.Dermatology,
                ConsultationFee = 180,
                Rating = 0,
                ScheduleRules = new List<DoctorScheduleRule>()
            };
            context.Doctors.Add(doctor);
            await context.SaveChangesAsync();

            DateTime today = DateTime.UtcNow.Date;
            await service.CreateScheduleRuleAsync(
                doctor.Id,
                ScheduleType.Available,
                FrequencyType.Daily,
                today,
                today.AddDays(7),
                TimeSpan.FromHours(8),
                TimeSpan.FromHours(16),
                ""
            );
            var rule = await context.DoctorScheduleRules.FirstOrDefaultAsync(r => r.DoctorId == doctor.Id);

            // Atualiza a regra para mudar o horário de término.
            await service.UpdateScheduleRuleAsync(
                rule.Id,
                doctor.Id,
                ScheduleType.Available,
                FrequencyType.Daily,
                today,
                today.AddDays(7),
                TimeSpan.FromHours(8),
                TimeSpan.FromHours(18),
                ""
            );

            var updatedRule = await context.DoctorScheduleRules.FirstOrDefaultAsync(r => r.Id == rule.Id);
            Assert.Equal(TimeSpan.FromHours(18), updatedRule.EndTimeOfDay);
            Assert.Empty(notificationService.Notifications);
        }

        [Fact]
        public async Task UpdateScheduleRule_ShouldAddNotification_WhenRuleNotFound()
        {
            using var context = CreateContext();
            await context.Database.MigrateAsync();
            var notificationService = new NotificationContextService();
            var cacheService = new FakeCacheService();
            var service = new SchedulingService(context, new AppointmentRepository(context), notificationService, cacheService);

            // Tenta atualizar uma regra inexistente.
            await service.UpdateScheduleRuleAsync(
                999, 1,
                ScheduleType.Available, FrequencyType.Daily,
                DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(7),
                TimeSpan.FromHours(8), TimeSpan.FromHours(17), ""
            );

            Assert.True(notificationService.HasNotifications);
            Assert.Contains(notificationService.Notifications, n => n.Key == "NotFound");
        }

        [Fact]
        public async Task UpdateScheduleRule_ShouldAddNotification_WhenNotAuthorized()
        {
            using var context = CreateContext();
            await context.Database.MigrateAsync();
            var notificationService = new NotificationContextService();
            var cacheService = new FakeCacheService();
            var service = new SchedulingService(context, new AppointmentRepository(context), notificationService, cacheService);

            // Cria dois médicos.
            var doctor1 = new Doctor
            {
                Name = "Dr. Authorized",
                LicenseNumber = "AUTH001",
                Specialty = Specialty.Cardiology,
                ConsultationFee = 150,
                Rating = 0,
                ScheduleRules = new List<DoctorScheduleRule>()
            };
            var doctor2 = new Doctor
            {
                Name = "Dr. Unauthorized",
                LicenseNumber = "UNAUTH001",
                Specialty = Specialty.Dermatology,
                ConsultationFee = 200,
                Rating = 0,
                ScheduleRules = new List<DoctorScheduleRule>()
            };
            context.Doctors.AddRange(doctor1, doctor2);
            await context.SaveChangesAsync();

            DateTime today = DateTime.UtcNow.Date;
            await service.CreateScheduleRuleAsync(
                doctor1.Id,
                ScheduleType.Available,
                FrequencyType.Daily,
                today,
                today.AddDays(7),
                TimeSpan.FromHours(8),
                TimeSpan.FromHours(16),
                ""
            );
            var rule = await context.DoctorScheduleRules.FirstOrDefaultAsync(r => r.DoctorId == doctor1.Id);

            // Tenta atualizar a regra com doctor2.
            await service.UpdateScheduleRuleAsync(
                rule.Id,
                doctor2.Id,
                ScheduleType.Available,
                FrequencyType.Daily,
                today,
                today.AddDays(7),
                TimeSpan.FromHours(8),
                TimeSpan.FromHours(16),
                ""
            );

            Assert.True(notificationService.HasNotifications);
            Assert.Contains(notificationService.Notifications, n => n.Key == "Unauthorized");
        }

        [Fact]
        public async Task GetAvailableTimeSlots_ShouldReturnSlot_WhenRuleCoversRequestedSlot()
        {
            using var context = CreateContext();
            await context.Database.MigrateAsync();
            var notificationService = new NotificationContextService();
            var appointmentRepository = new AppointmentRepository(context);
            var cacheService = new FakeCacheService();
            var service = new SchedulingService(context, appointmentRepository, notificationService, cacheService);

            // Cria um médico com uma regra de disponibilidade válida.
            var doctor = new Doctor
            {
                Name = "Dr. Availability",
                LicenseNumber = "AV001",
                Specialty = Specialty.GeneralMedicine,
                ConsultationFee = 120,
                Rating = 0,
                ScheduleRules = new List<DoctorScheduleRule>()
            };
            context.Doctors.Add(doctor);

            // Cria um paciente (necessário para agendamentos).
            var patient = new Patient
            {
                Name = "Test Patient",
                Email = "testpatient@example.com",
                NationalId = "00000000000",
                PasswordHash = "dummyHash",
                PasswordSalt = "dummySalt"
            };
            context.Patients.Add(patient);
            await context.SaveChangesAsync();

            DateTime today = DateTime.UtcNow.Date;
            await service.CreateScheduleRuleAsync(
                doctor.Id,
                ScheduleType.Available,
                FrequencyType.Daily,
                today,
                today.AddDays(7),
                TimeSpan.FromHours(8),
                TimeSpan.FromHours(17),
                ""
            );

            DateTime startSearch = today.AddDays(1).AddHours(9);
            DateTime endSearch = today.AddDays(1).AddHours(17);
            var slots = await service.GetAvailableTimeSlotsAsync(doctor.Id, startSearch, endSearch);

            Assert.NotEmpty(slots);
            Assert.Contains(slots, slot => slot == new DateTime(startSearch.Year, startSearch.Month, startSearch.Day, 9, 0, 0));
        }

        [Fact]
        public async Task GetAvailableTimeSlots_ShouldNotReturnSlot_WhenBlockedRuleExists()
        {
            using var context = CreateContext();
            await context.Database.MigrateAsync();
            var notificationService = new NotificationContextService();
            var appointmentRepository = new AppointmentRepository(context);
            var cacheService = new FakeCacheService();
            var service = new SchedulingService(context, appointmentRepository, notificationService, cacheService);

            // Cria um médico com regra de disponibilidade e, em seguida, uma regra de bloqueio.
            var doctor = new Doctor
            {
                Name = "Dr. Blocked",
                LicenseNumber = "BLK001",
                Specialty = Specialty.GeneralMedicine,
                ConsultationFee = 130,
                Rating = 0,
                ScheduleRules = new List<DoctorScheduleRule>()
            };
            context.Doctors.Add(doctor);

            // Cria um paciente.
            var patient = new Patient
            {
                Name = "Test Patient",
                Email = "testpatient@example.com",
                NationalId = "00000000000",
                PasswordHash = "dummyHash",
                PasswordSalt = "dummySalt"
            };
            context.Patients.Add(patient);
            await context.SaveChangesAsync();

            DateTime today = DateTime.UtcNow.Date;
            // Regra de disponibilidade: 8h - 17h.
            await service.CreateScheduleRuleAsync(
                doctor.Id,
                ScheduleType.Available,
                FrequencyType.Daily,
                today,
                today.AddDays(7),
                TimeSpan.FromHours(8),
                TimeSpan.FromHours(17),
                ""
            );
            // Regra de bloqueio: 9h - 9h30.
            await service.CreateScheduleRuleAsync(
                doctor.Id,
                ScheduleType.Blocked,
                FrequencyType.Daily,
                today,
                today.AddDays(7),
                TimeSpan.FromHours(9),
                TimeSpan.FromHours(9.5),
                ""
            );

            DateTime startSearch = today.AddDays(1).AddHours(8);
            DateTime endSearch = today.AddDays(1).AddHours(17);
            var slots = await service.GetAvailableTimeSlotsAsync(doctor.Id, startSearch, endSearch);

            // O horário 9:00 não deve estar disponível.
            Assert.DoesNotContain(slots, slot => slot == new DateTime(startSearch.Year, startSearch.Month, startSearch.Day, 9, 0, 0));
        }

        [Fact]
        public async Task GetDoctorFullCalendar_ShouldReturnConsolidatedEvents()
        {
            using var context = CreateContext();
            await context.Database.MigrateAsync();

            // Cria os serviços necessários.
            var notificationService = new NotificationContextService();
            var appointmentRepository = new AppointmentRepository(context);
            var cacheService = new FakeCacheService();
            var service = new SchedulingService(context, appointmentRepository, notificationService, cacheService);

            // Cria um médico e um paciente.
            var doctor = new Doctor
            {
                Name = "Dr. Calendar",
                LicenseNumber = "CAL001",
                Specialty = Specialty.GeneralMedicine,
                ConsultationFee = 100,
                Rating = 5,
                ScheduleRules = new List<DoctorScheduleRule>()
            };
            context.Doctors.Add(doctor);

            var patient = new Patient
            {
                Name = "Calendar Patient",
                Email = "calendarpatient@example.com",
                NationalId = "11111111111",
                PasswordHash = "dummyHash",
                PasswordSalt = "dummySalt"
            };
            context.Patients.Add(patient);
            await context.SaveChangesAsync();

            // Define o dia de teste (por exemplo, amanhã).
            DateTime day = DateTime.UtcNow.Date.AddDays(1);

            // Cria uma regra de disponibilidade: 8h às 17h.
            await service.CreateScheduleRuleAsync(
                doctor.Id,
                ScheduleType.Available,
                FrequencyType.Daily,
                day,
                day.AddDays(1),
                TimeSpan.FromHours(8),
                TimeSpan.FromHours(17),
                ""
            );

            // Cria uma regra de bloqueio: 9h às 9h30.
            await service.CreateScheduleRuleAsync(
                doctor.Id,
                ScheduleType.Blocked,
                FrequencyType.Daily,
                day,
                day.AddDays(1),
                TimeSpan.FromHours(9),
                TimeSpan.FromHours(9.5),
                ""
            );

            // Agenda um atendimento às 10h (duração fixa de 30 minutos).
            DateTime appointmentTime = day.AddHours(10);
            var appointment = await service.ScheduleAppointmentAsync(doctor.Id, patient.Id, appointmentTime, "uniqueCalendarTest");
            Assert.NotNull(appointment);

            // Define o intervalo para a consulta do calendário (das 8h às 17h do dia).
            DateTime rangeStart = day.AddHours(8);
            DateTime rangeEnd = day.AddHours(17);

            // Obtém o calendário consolidado.
            DoctorFullCalendarDto calendar = await service.GetDoctorFullCalendarAsync(doctor.Id, rangeStart, rangeEnd);

            Assert.NotNull(calendar);
            Assert.Equal(doctor.Id, calendar.DoctorId);
            Assert.NotEmpty(calendar.Events);

            // Para o cenário, espera-se que os eventos sejam divididos em 5 segmentos:
            // 1. 08:00 - 09:00: Available
            // 2. 09:00 - 09:30: Blocked
            // 3. 09:30 - 10:00: Available
            // 4. 10:00 - 10:30: Appointment
            // 5. 10:30 - 17:00: Available
            Assert.Equal(5, calendar.Events.Count);

            // Verifica o segmento bloqueado.
            CalendarEventDto blockedEvent = calendar.Events.FirstOrDefault(e => e.Type == "Blocked");
            Assert.NotNull(blockedEvent);
            Assert.Equal(day.AddHours(9), blockedEvent.Start);
            Assert.Equal(day.AddHours(9.5), blockedEvent.End);

            // Verifica o segmento do agendamento.
            CalendarEventDto appointmentEvent = calendar.Events.FirstOrDefault(e => e.Type == "Appointment");
            Assert.NotNull(appointmentEvent);
            Assert.Equal(day.AddHours(10), appointmentEvent.Start);
            Assert.Equal(day.AddHours(10).AddMinutes(30), appointmentEvent.End);

            // Opcional: Verifica os segmentos "Available"
            var availableEvents = calendar.Events.Where(e => e.Type == "Available").ToList();
            Assert.Equal(3, availableEvents.Count);
        }
    }
}
