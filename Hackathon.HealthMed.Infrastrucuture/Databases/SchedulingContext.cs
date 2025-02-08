using Hackathon.HealthMed.Domain.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hackathon.HealthMed.Infrastrucuture.Databases
{
    public class SchedulingContext : DbContext
    {
        public SchedulingContext(DbContextOptions<SchedulingContext> options)
            : base(options)
        {
        }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<DoctorScheduleRule> DoctorScheduleRules { get; set; }
        public DbSet<DoctorReview> DoctorReviews { get; set; }
        public DbSet<DoctorStatistics> DoctorStatistics { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Appointment>()
                .HasIndex(a => a.UniqueRequestId)
                .IsUnique(false);

            modelBuilder.Entity<Doctor>()
                .HasOne(d => d.Statistics)
                .WithOne(s => s.Doctor)
                .HasForeignKey<DoctorStatistics>(s => s.DoctorId);

            SetupUTCDateTime(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }

        private void SetupUTCDateTime(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var properties = entityType.ClrType.GetProperties()
                    .Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?));

                foreach (var property in properties)
                {
                    modelBuilder.Entity(entityType.Name).Property(property.Name)
                        .HasConversion(new ValueConverter<DateTime?, DateTime?>(
                            v => v.HasValue ? DateTime.SpecifyKind(v.Value.ToUniversalTime(), DateTimeKind.Utc) : v,
                            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v
                        ));
                }
            }
        }
    }
}
