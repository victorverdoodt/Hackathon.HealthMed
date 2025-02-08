using Hackathon.HealthMed.Application.DTO.Models.Requests;
using Hackathon.HealthMed.Infrastrucuture.Databases;
using Hackathon.HealthMed.Infrastrucuture.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hackathon.HealthMed.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly SchedulingContext _context;
        public AuthController(SchedulingContext context)
        {
            _context = context;
        }

        [HttpPost("doctor/login")]
        public async Task<IActionResult> DoctorLogin([FromBody] DoctorLoginRequest request)
        {
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.LicenseNumber == request.LicenseNumber);
            if (doctor == null)
                return BadRequest("Invalid credentials");
            if (!PasswordHasher.VerifyPasswordHash(request.Password, doctor.PasswordHash, doctor.PasswordSalt))
                return BadRequest("Invalid credentials");
            var token = JwtHelper.GenerateJwtToken(doctor.Id.ToString(), "Doctor");
            return Ok(new { token });
        }

        [HttpPost("patient/login")]
        public async Task<IActionResult> PatientLogin([FromBody] PatientLoginRequest request)
        {
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Email == request.Email);
            if (patient == null)
                return BadRequest("Invalid credentials");
            if (!PasswordHasher.VerifyPasswordHash(request.Password, patient.PasswordHash, patient.PasswordSalt))
                return BadRequest("Invalid credentials");
            var token = JwtHelper.GenerateJwtToken(patient.Id.ToString(), "Patient");
            return Ok(new { token });
        }
    }

}
