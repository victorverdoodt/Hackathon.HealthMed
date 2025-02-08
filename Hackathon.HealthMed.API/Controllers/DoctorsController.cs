using Hackathon.HealthMed.Domain.Models.Base;
using Hackathon.HealthMed.Domain.Models.Entities;
using Hackathon.HealthMed.Domain.Models.Enum;
using Hackathon.HealthMed.Infrastrucuture.Databases;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hackathon.HealthMed.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DoctorsController : ControllerBase
    {
        private readonly SchedulingContext _context;
        public DoctorsController(SchedulingContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> SearchDoctors(
            [FromQuery] string specialty,
            [FromQuery] double? minRating,
            [FromQuery] double? latitude,
            [FromQuery] double? longitude,
            [FromQuery] double? maxDistance,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string sortBy = "name",
            [FromQuery] string sortOrder = "asc")
        {
            var query = _context.Doctors.AsQueryable();
            if (!string.IsNullOrWhiteSpace(specialty))
            {
                if (Enum.TryParse(typeof(Specialty), specialty, true, out var parsedSpecialty))
                    query = query.Where(d => d.Specialty == (Specialty)parsedSpecialty);
                else
                    query = query.Where(d => d.Specialty.ToString().Contains(specialty, StringComparison.OrdinalIgnoreCase));
            }
            if (minRating.HasValue)
                query = query.Where(d => d.Rating >= minRating.Value);

            var doctors = await query.ToListAsync();
            if (maxDistance.HasValue && latitude.HasValue && longitude.HasValue)
            {
                doctors = doctors.Where(d =>
                {
                    double distance = CalculateDistance(latitude.Value, longitude.Value, d.Latitude, d.Longitude);
                    return distance <= maxDistance.Value;
                }).ToList();
            }
            // Ordenação simples
            doctors = sortBy.ToLower() switch
            {
                "rating" => sortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase) ? doctors.OrderByDescending(d => d.Rating).ToList() : doctors.OrderBy(d => d.Rating).ToList(),
                "consultationfee" => sortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase) ? doctors.OrderByDescending(d => d.ConsultationFee).ToList() : doctors.OrderBy(d => d.ConsultationFee).ToList(),
                "specialty" => sortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase) ? doctors.OrderByDescending(d => d.Specialty.ToString()).ToList() : doctors.OrderBy(d => d.Specialty.ToString()).ToList(),
                _ => doctors.OrderBy(d => d.Name).ToList()
            };
            int totalItems = doctors.Count;
            var pagedDoctors = doctors.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var result = new PaginatedResult<Doctor>
            {
                Items = pagedDoctors,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            };
            return Ok(result);
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            double latRad1 = lat1 * Math.PI / 180;
            double latRad2 = lat2 * Math.PI / 180;
            double deltaLat = (lat2 - lat1) * Math.PI / 180;
            double deltaLon = (lon2 - lon1) * Math.PI / 180;
            double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                       Math.Cos(latRad1) * Math.Cos(latRad2) *
                       Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}
