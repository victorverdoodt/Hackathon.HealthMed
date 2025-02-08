using Hackathon.HealthMed.Domain.Models.Entities;

namespace Hackathon.HealthMed.Infrastrucuture.Repositories
{
    public interface IAppointmentRepository
    {
        Task<Appointment> GetAppointmentByUniqueRequestIdAsync(string uniqueRequestId);
        Task AddAppointmentAsync(Appointment appointment);
        Task<bool> HasConflictAsync(int doctorId, DateTime start, DateTime end);
        Task<int> CountAcceptedAppointmentsAsync(int doctorId);
    }
}
