namespace Hackathon.HealthMed.Application.DTO.Models.Response
{
    public class CalendarEventDto
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
    }

    public class DoctorFullCalendarDto
    {
        public int DoctorId { get; set; }
        public List<CalendarEventDto> Events { get; set; }
    }
}
