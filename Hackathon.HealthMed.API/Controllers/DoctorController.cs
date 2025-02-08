using Hackathon.HealthMed.Application.DTO.Models.Requests;
using Hackathon.HealthMed.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Hackathon.HealthMed.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DoctorController : ControllerBase
    {
        private readonly SchedulingService _service;
        public DoctorController(SchedulingService service)
        {
            _service = service;
        }

        [Authorize(Roles = "Doctor")]
        [HttpGet("schedule-rules")]
        public async Task<IActionResult> GetScheduleRules()
        {
            var doctorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            int doctorId = int.Parse(doctorIdClaim.Value);
            var rules = await _service.GetAllScheduleRulesForDoctorAsync(doctorId);
            return Ok(rules);
        }

        [Authorize(Roles = "Doctor")]
        [HttpPost("schedule-rules")]
        public async Task<IActionResult> CreateScheduleRule([FromBody] CreateScheduleRuleRequest request)
        {
            var doctorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            int doctorId = int.Parse(doctorIdClaim.Value);
            await _service.CreateScheduleRuleAsync(
                doctorId,
                request.ScheduleType,
                request.FrequencyType,
                request.StartDate,
                request.EndDate,
                request.StartTimeOfDay,
                request.EndTimeOfDay,
                request.DaysOfWeek);
            return Ok("Rule created successfully.");
        }

        [Authorize(Roles = "Doctor")]
        [HttpPut("schedule-rules/{ruleId}")]
        public async Task<IActionResult> UpdateScheduleRule([FromRoute] int ruleId, [FromBody] UpdateScheduleRuleRequest request)
        {
            var doctorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            int doctorId = int.Parse(doctorIdClaim.Value);
            await _service.UpdateScheduleRuleAsync(
                ruleId,
                doctorId,
                request.ScheduleType,
                request.FrequencyType,
                request.StartDate,
                request.EndDate,
                request.StartTimeOfDay,
                request.EndTimeOfDay,
                request.DaysOfWeek);
            return Ok("Rule updated successfully.");
        }

        [Authorize(Roles = "Doctor")]
        [HttpPost("appointments/{appointmentId}/accept")]
        public async Task<IActionResult> AcceptAppointment([FromRoute] int appointmentId)
        {
            var doctorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            int doctorId = int.Parse(doctorIdClaim.Value);
            var appointment = await _service.AcceptAppointmentAsync(appointmentId, doctorId);
            return Ok(appointment);
        }

        [Authorize(Roles = "Doctor")]
        [HttpPost("appointments/{appointmentId}/reject")]
        public async Task<IActionResult> RejectAppointment([FromRoute] int appointmentId)
        {
            var doctorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            int doctorId = int.Parse(doctorIdClaim.Value);
            var appointment = await _service.RejectAppointmentAsync(appointmentId, doctorId);
            return Ok(appointment);
        }

        [HttpGet("{doctorId}/fullcalendar")]
        public async Task<IActionResult> GetFullCalendar(
            int doctorId,
            [FromQuery] DateTime rangeStart,
            [FromQuery] DateTime rangeEnd)
        {
            var calendar = await _service.GetDoctorFullCalendarAsync(doctorId, rangeStart, rangeEnd);

            return Ok(calendar);
        }
    }
}
