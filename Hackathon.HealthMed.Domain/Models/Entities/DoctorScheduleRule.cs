using Hackathon.HealthMed.Domain.Models.Enum;
using System.ComponentModel.DataAnnotations;

namespace Hackathon.HealthMed.Domain.Models.Entities
{
    public class DoctorScheduleRule
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; }
        public ScheduleType ScheduleType { get; set; }
        public FrequencyType FrequencyType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string DaysOfWeek { get; set; }
        public TimeSpan StartTimeOfDay { get; set; }
        public TimeSpan EndTimeOfDay { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; }
    }
}
