using Hackathon.HealthMed.Domain.Models.Enum;

namespace Hackathon.HealthMed.Application.DTO.Models.Requests
{
    public class CreateScheduleRuleRequest
    {
        public ScheduleType ScheduleType { get; set; }
        public FrequencyType FrequencyType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public TimeSpan StartTimeOfDay { get; set; }
        public TimeSpan EndTimeOfDay { get; set; }
        public string DaysOfWeek { get; set; }
    }
}
