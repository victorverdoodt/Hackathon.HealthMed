namespace Hackathon.HealthMed.Application.DTO.Models.Requests
{
    public class AppointmentRequest
    {
        public int DoctorId { get; set; }
        public int PatientId { get; set; } // Será obtido do token
        public DateTime TimeSlot { get; set; }
        public string UniqueRequestId { get; set; }
    }
}
