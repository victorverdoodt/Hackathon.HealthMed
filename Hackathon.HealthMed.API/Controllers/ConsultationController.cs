using Hackathon.HealthMed.Application.DTO.Models.Requests;
using Hackathon.HealthMed.Infrastrucuture.Databases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Hackathon.HealthMed.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConsultationController : ControllerBase
    {
        private readonly SchedulingContext _context;
        public ConsultationController(SchedulingContext context)
        {
            _context = context;
        }
        [Authorize(Roles = "Doctor")]
        [HttpPut("fee")]
        public async Task<IActionResult> UpdateConsultationFee([FromBody] UpdateConsultationFeeRequest request)
        {
            var doctorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            int doctorId = int.Parse(doctorIdClaim.Value);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == doctorId);
            doctor.ConsultationFee = request.ConsultationFee;
            await _context.SaveChangesAsync();
            return Ok("Consultation fee updated successfully.");
        }
    }
}
