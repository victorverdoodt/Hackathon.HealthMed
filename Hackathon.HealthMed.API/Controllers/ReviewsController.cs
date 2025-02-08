using Hackathon.HealthMed.Application.DTO.Models.Requests;
using Hackathon.HealthMed.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Hackathon.HealthMed.API.Controllers
{
    [ApiController]
    [Route("api/doctors/{doctorId}/reviews")]
    public class ReviewsController : ControllerBase
    {
        private readonly SchedulingService _service;
        public ReviewsController(SchedulingService service)
        {
            _service = service;
        }
        [HttpGet]
        public async Task<IActionResult> GetReviews([FromRoute] int doctorId)
        {
            var reviews = await _service.GetDoctorReviewsAsync(doctorId);
            return Ok(reviews);
        }
        [Authorize(Roles = "Patient")]
        [HttpPost]
        public async Task<IActionResult> AddReview([FromRoute] int doctorId, [FromBody] DoctorReviewRequest request)
        {
            var patientIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            int patientId = int.Parse(patientIdClaim.Value);
            var review = await _service.AddDoctorReviewAsync(doctorId, patientId, request.Rating, request.Comment);
            return Ok(review);
        }
    }
}
