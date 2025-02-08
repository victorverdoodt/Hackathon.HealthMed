using Hackathon.HealthMed.Domain.Models.Enum;

namespace Hackathon.HealthMed.Domain.Models.Entities
{
    public class Doctor
    {
        public int Id { get; set; }
        public string Name { get; set; }
        // Propriedades seguras de autenticação
        public string PasswordHash { get; set; }
        public string PasswordSalt { get; set; }
        public Specialty Specialty { get; set; }
        public double Rating { get; set; }  // Média das avaliações
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public decimal ConsultationFee { get; set; } // Valor da consulta
        public ICollection<DoctorScheduleRule> ScheduleRules { get; set; }
        public ICollection<Appointment> Appointments { get; set; }
        public ICollection<DoctorReview> Reviews { get; set; }
        public DoctorStatistics Statistics { get; set; }
        public string LicenseNumber { get; set; }
    }
}
