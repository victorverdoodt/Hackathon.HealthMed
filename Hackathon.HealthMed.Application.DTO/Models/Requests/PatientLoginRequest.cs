namespace Hackathon.HealthMed.Application.DTO.Models.Requests
{
    public class PatientLoginRequest
    {
        public string Email { get; set; }
        public string NationalId { get; set; }
        public string Password { get; set; }
    }
}
