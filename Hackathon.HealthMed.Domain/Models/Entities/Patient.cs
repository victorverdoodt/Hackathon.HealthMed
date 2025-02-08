namespace Hackathon.HealthMed.Domain.Models.Entities
{
    public class Patient
    {
        public int Id { get; set; }
        public string Name { get; set; }
        // Propriedades seguras de autenticação
        public string PasswordHash { get; set; }
        public string PasswordSalt { get; set; }
        public string Email { get; set; }
        public string NationalId { get; set; }
        public ICollection<Appointment> Appointments { get; set; }
    }
}
