using System.ComponentModel.DataAnnotations;

namespace Hackathon.HealthMed.Domain.Models.Entities
{
    public class DoctorStatistics
    {
        [Key]
        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; }
        public double AverageRating { get; set; }
        public int TotalAppointments { get; set; }
    }
}
