using Hackathon.HealthMed.Domain.Models.Enum;
using System.ComponentModel.DataAnnotations;

namespace Hackathon.HealthMed.Domain.Models.Entities
{
    public class Appointment
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; }
        public int PatientId { get; set; }
        public Patient Patient { get; set; }
        [Required]
        public DateTime StartDateTime { get; set; }
        public AppointmentStatus Status { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; }
        public string UniqueRequestId { get; set; }
        // Justificativa para cancelamento (inserida pelo paciente)
        public string CancellationJustification { get; set; }
    }
}
