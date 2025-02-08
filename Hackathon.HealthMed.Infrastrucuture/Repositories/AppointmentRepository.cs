using Hackathon.HealthMed.Domain.Models.Entities;
using Hackathon.HealthMed.Domain.Models.Enum;
using Hackathon.HealthMed.Infrastrucuture.Databases;
using Microsoft.EntityFrameworkCore;

namespace Hackathon.HealthMed.Infrastrucuture.Repositories
{
    public class AppointmentRepository : IAppointmentRepository
    {
        private readonly SchedulingContext _context;
        public AppointmentRepository(SchedulingContext context)
        {
            _context = context;
        }
        public async Task<Appointment> GetAppointmentByUniqueRequestIdAsync(string uniqueRequestId)
        {
            return await _context.Appointments.FirstOrDefaultAsync(a => a.UniqueRequestId == uniqueRequestId);
        }
        public async Task AddAppointmentAsync(Appointment appointment)
        {
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();
        }
        public async Task<bool> HasConflictAsync(int doctorId, DateTime start, DateTime end)
        {
            return await _context.Appointments.AnyAsync(a =>
                a.DoctorId == doctorId &&
                a.Status == AppointmentStatus.Scheduled &&
                a.StartDateTime < end &&
                a.StartDateTime.AddMinutes(30) > start);
        }
        public async Task<int> CountAcceptedAppointmentsAsync(int doctorId)
        {
            return await _context.Appointments.CountAsync(a =>
                a.DoctorId == doctorId && a.Status == AppointmentStatus.Accepted);
        }
    }
}

