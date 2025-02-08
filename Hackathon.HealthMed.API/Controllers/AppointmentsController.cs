using Hackathon.HealthMed.Application.DTO.Models.Requests;
using Hackathon.HealthMed.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Hackathon.HealthMed.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppointmentsController : ControllerBase
    {
        private readonly SchedulingService _service;
        public AppointmentsController(SchedulingService service)
        {
            _service = service;
        }

        [Authorize(Roles = "Patient")]
        [HttpPost]
        public async Task<IActionResult> ScheduleAppointment([FromBody] AppointmentRequest request)
        {
            var patientIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (patientIdClaim == null)
                return BadRequest("Patient not identified.");
            request.PatientId = int.Parse(patientIdClaim.Value);
            var appointment = await _service.ScheduleAppointmentAsync(request.DoctorId, request.PatientId, request.TimeSlot, request.UniqueRequestId);
            return Ok(appointment);
        }

        [Authorize(Roles = "Patient")]
        [HttpGet("available-slots")]
        public async Task<IActionResult> GetAvailableSlots([FromQuery] int doctorId, [FromQuery] DateTime rangeStart, [FromQuery] DateTime rangeEnd)
        {
            var slots = await _service.GetAvailableTimeSlotsAsync(doctorId, rangeStart, rangeEnd);
            return Ok(slots);
        }

        [Authorize(Roles = "Patient")]
        [HttpDelete("{appointmentId}")]
        public async Task<IActionResult> CancelAppointment([FromRoute] int appointmentId, [FromBody] CancelAppointmentRequest request)
        {
            var patientIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (patientIdClaim == null)
                return BadRequest("Patient not identified.");
            int patientId = int.Parse(patientIdClaim.Value);
            var appointment = await _service.CancelAppointmentAsync(appointmentId, patientId, request.Justification);
            return Ok(appointment);
        }
    }
}
