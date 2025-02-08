using Hackathon.HealthMed.Application.DTO.Models.Response;
using Hackathon.HealthMed.Domain.Models.Entities;
using Hackathon.HealthMed.Domain.Models.Enum;
using Hackathon.HealthMed.Domain.Models.Interfaces;
using Hackathon.HealthMed.Infrastrucuture.Databases;
using Hackathon.HealthMed.Infrastrucuture.Repositories;
using Hackathon.HealthMed.Infrastrucuture.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Hackathon.HealthMed.Application.Services
{
    public class SchedulingService
    {
        private const int APPOINTMENT_DURATION_MINUTES = 30;
        private readonly SchedulingContext _context;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly NotificationContextService _notificationContext;
        private readonly ICacheService _cacheService;

        public SchedulingService(
            SchedulingContext context, 
            IAppointmentRepository appointmentRepository, 
            NotificationContextService notificationContext,
            ICacheService cacheService)
        {
            _context = context;
            _appointmentRepository = appointmentRepository;
            _notificationContext = notificationContext;
            _cacheService = cacheService;
        }

        // --- Schedule Rules Management ---
        public async Task CreateScheduleRuleAsync(int doctorId, ScheduleType scheduleType, FrequencyType frequencyType,
            DateTime startDate, DateTime? endDate, TimeSpan startTimeOfDay, TimeSpan endTimeOfDay, string daysOfWeek)
        {
            if (endTimeOfDay <= startTimeOfDay)
            {
                _notificationContext.AddNotification("InvalidTime", "End time must be after start time.");
                return;
            }
            if (endDate.HasValue && endDate.Value < startDate)
            {
                _notificationContext.AddNotification("InvalidDate", "End date cannot be before start date.");
                return;
            }
            var rule = new DoctorScheduleRule
            {
                DoctorId = doctorId,
                ScheduleType = scheduleType,
                FrequencyType = frequencyType,
                StartDate = startDate.Date,
                EndDate = endDate?.Date,
                StartTimeOfDay = startTimeOfDay,
                EndTimeOfDay = endTimeOfDay,
                DaysOfWeek = daysOfWeek
            };
            _context.DoctorScheduleRules.Add(rule);
            await _context.SaveChangesAsync();
            await _cacheService.InvalidateCacheByKeyAsync($"GetAvailableTimeSlotsAsync:{doctorId}");
        }

        public async Task UpdateScheduleRuleAsync(int ruleId, int doctorId, ScheduleType scheduleType, FrequencyType frequencyType,
            DateTime startDate, DateTime? endDate, TimeSpan startTimeOfDay, TimeSpan endTimeOfDay, string daysOfWeek)
        {
            var rule = await _context.DoctorScheduleRules.FirstOrDefaultAsync(r => r.Id == ruleId);
            if (rule == null)
            {
                _notificationContext.AddNotification("NotFound", "Schedule rule not found.");
                return;
            }
            if (rule.DoctorId != doctorId)
            {
                _notificationContext.AddNotification("Unauthorized", "You are not allowed to update this schedule rule.");
                return;
            }
            if (endTimeOfDay <= startTimeOfDay)
            {
                _notificationContext.AddNotification("InvalidTime", "End time must be after start time.");
                return;
            }
            if (endDate.HasValue && endDate.Value < startDate)
            {
                _notificationContext.AddNotification("InvalidDate", "End date cannot be before start date.");
                return;
            }
            rule.ScheduleType = scheduleType;
            rule.FrequencyType = frequencyType;
            rule.StartDate = startDate.Date;
            rule.EndDate = endDate?.Date;
            rule.StartTimeOfDay = startTimeOfDay;
            rule.EndTimeOfDay = endTimeOfDay;
            rule.DaysOfWeek = daysOfWeek;
            await _context.SaveChangesAsync();
            await _cacheService.InvalidateCacheByKeyAsync($"GetAvailableTimeSlotsAsync:{doctorId}");
        }

        // --- Appointment Management ---
        public async Task<Appointment> ScheduleAppointmentAsync(int doctorId, int patientId, DateTime requestedStart, string uniqueRequestId)
        {
            if ((requestedStart.Date - DateTime.UtcNow.Date).TotalDays > 14)
            {
                _notificationContext.AddNotification("AvailabilityRangeTooLong", "Search for availability is limited to 2 weeks.");
                return null;
            }
            DateTime start = AlignToThirtyMinuteBoundary(requestedStart);
            DateTime end = start.AddMinutes(APPOINTMENT_DURATION_MINUTES);

            var existing = await _appointmentRepository.GetAppointmentByUniqueRequestIdAsync(uniqueRequestId);
            if (existing != null)
                return existing;

            // Verifica se o horário solicitado está coberto por uma regra de disponibilidade.
            bool isAvailable = await IsCoveredByAvailabilityOnTheFlyAsync(doctorId, start, end);
            if (!isAvailable)
            {
                _notificationContext.AddNotification("AvailabilityError", "Requested time slot is not fully available.");
                return null;
            }

            bool conflict = await _appointmentRepository.HasConflictAsync(doctorId, start, end);
            if (conflict)
            {
                _notificationContext.AddNotification("ConflictError", "An appointment already exists in that time slot.");
                return null;
            }

            var appointment = new Appointment
            {
                DoctorId = doctorId,
                PatientId = patientId,
                StartDateTime = start,
                Status = AppointmentStatus.Scheduled,
                UniqueRequestId = uniqueRequestId
            };

            await _appointmentRepository.AddAppointmentAsync(appointment);
            await _cacheService.InvalidateCacheByKeyAsync($"GetAvailableTimeSlotsAsync:{doctorId}");
            return appointment;
        }

        public async Task<Appointment> AcceptAppointmentAsync(int appointmentId, int doctorId)
        {
            var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);
            if (appointment == null)
            {
                _notificationContext.AddNotification("NotFound", "Appointment not found.");
                return null;
            }
            if (appointment.DoctorId != doctorId)
            {
                _notificationContext.AddNotification("Unauthorized", "Appointment does not belong to the doctor.");
                return null;
            }
            if (appointment.Status != AppointmentStatus.Scheduled)
            {
                _notificationContext.AddNotification("InvalidStatus", "Only scheduled appointments can be accepted.");
                return null;
            }
            appointment.Status = AppointmentStatus.Accepted;
            await _context.SaveChangesAsync();
            await _cacheService.InvalidateCacheByKeyAsync($"GetAvailableTimeSlotsAsync:{appointment.DoctorId}");
            return appointment;
        }

        public async Task<Appointment> RejectAppointmentAsync(int appointmentId, int doctorId)
        {
            var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);
            if (appointment == null)
            {
                _notificationContext.AddNotification("NotFound", "Appointment not found.");
                return null;
            }
            if (appointment.DoctorId != doctorId)
            {
                _notificationContext.AddNotification("Unauthorized", "Appointment does not belong to the doctor.");
                return null;
            }
            if (appointment.Status != AppointmentStatus.Scheduled)
            {
                _notificationContext.AddNotification("InvalidStatus", "Only scheduled appointments can be rejected.");
                return null;
            }
            appointment.Status = AppointmentStatus.Rejected;
            await _context.SaveChangesAsync();
            await _cacheService.InvalidateCacheByKeyAsync($"GetAvailableTimeSlotsAsync:{appointment.DoctorId}");
            return appointment;
        }

        public async Task<Appointment> CancelAppointmentAsync(int appointmentId, int patientId, string justification)
        {
            var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);
            if (appointment == null)
            {
                _notificationContext.AddNotification("NotFound", "Appointment not found.");
                return null;
            }
            if (appointment.PatientId != patientId)
            {
                _notificationContext.AddNotification("Unauthorized", "This appointment does not belong to the patient.");
                return null;
            }
            if (appointment.Status != AppointmentStatus.Scheduled && appointment.Status != AppointmentStatus.Accepted)
            {
                _notificationContext.AddNotification("InvalidStatus", "Only scheduled or accepted appointments can be canceled.");
                return null;
            }
            if (string.IsNullOrWhiteSpace(justification))
            {
                _notificationContext.AddNotification("JustificationRequired", "Cancellation justification is required.");
                return null;
            }
            appointment.Status = AppointmentStatus.Canceled;
            appointment.CancellationJustification = justification;
            await _context.SaveChangesAsync();
            await _cacheService.InvalidateCacheByKeyAsync($"GetAvailableTimeSlotsAsync:{appointment.DoctorId}");
            return appointment;
        }

        // --- Doctor Reviews ---
        public async Task<DoctorReview> AddDoctorReviewAsync(int doctorId, int patientId, double rating, string comment)
        {
            var review = new DoctorReview
            {
                DoctorId = doctorId,
                PatientId = patientId,
                Rating = rating,
                Comment = comment,
                CreatedAt = DateTime.UtcNow
            };
            _context.DoctorReviews.Add(review);
            await _context.SaveChangesAsync();

            var reviews = await _context.DoctorReviews.Where(r => r.DoctorId == doctorId).ToListAsync();
            double avgRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == doctorId);
            if (doctor != null)
            {
                doctor.Rating = avgRating;
                await _context.SaveChangesAsync();
            }
            return review;
        }

        public async Task<List<DoctorReview>> GetDoctorReviewsAsync(int doctorId)
        {
            return await _context.DoctorReviews.Include(r => r.Patient)
                .Where(r => r.DoctorId == doctorId).ToListAsync();
        }

        // --- Consolidation with Hangfire Worker ---
        public async Task ConsolidateDoctorStatisticsAsync()
        {
            var doctors = await _context.Doctors.ToListAsync();
            foreach (var doctor in doctors)
            {
                var reviews = await _context.DoctorReviews.Where(r => r.DoctorId == doctor.Id).ToListAsync();
                double avgRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;
                int totalAppointments = await _appointmentRepository.CountAcceptedAppointmentsAsync(doctor.Id);
                var stats = await _context.DoctorStatistics.FirstOrDefaultAsync(s => s.DoctorId == doctor.Id);
                if (stats == null)
                {
                    stats = new DoctorStatistics
                    {
                        DoctorId = doctor.Id,
                        AverageRating = avgRating,
                        TotalAppointments = totalAppointments
                    };
                    _context.DoctorStatistics.Add(stats);
                }
                else
                {
                    stats.AverageRating = avgRating;
                    stats.TotalAppointments = totalAppointments;
                }
            }
            await _context.SaveChangesAsync();
        }

        // --- Available Time Slots ---
        public async Task<List<DateTime>> GetAvailableTimeSlotsAsync(int doctorId, DateTime rangeStart, DateTime rangeEnd)
        {
            if ((rangeEnd - rangeStart).TotalDays > 14)
            {
                _notificationContext.AddNotification("AvailabilityRangeTooLong", "Search for availability is limited to 2 weeks.");
                return null;
            }

            var result = await _cacheService.GetOrAddAsync($"GetAvailableTimeSlotsAsync:{doctorId}", async () =>
            {
                var results = new List<DateTime>();
                var dayToCheck = rangeStart.Date;
                var lastDay = rangeEnd.Date;
                while (dayToCheck <= lastDay)
                {
                    var mergedAvailable = await GetMergedAvailableIntervalsForDayAsync(doctorId, dayToCheck);
                    var mergedBlocked = await GetMergedBlockedIntervalsForDayAsync(doctorId, dayToCheck);
                    var dayAppointments = await _context.Appointments.Where(a => a.DoctorId == doctorId &&
                            a.Status == AppointmentStatus.Scheduled &&
                            a.StartDateTime.Date == dayToCheck).ToListAsync();
                    foreach (var (intervalStart, intervalEnd) in mergedAvailable)
                    {
                        var current = intervalStart;
                        while (current < intervalEnd)
                        {
                            var slotEnd = current.AddMinutes(APPOINTMENT_DURATION_MINUTES);
                            if (slotEnd > intervalEnd)
                                break;
                            if (current < rangeStart || slotEnd > rangeEnd)
                            {
                                current = current.AddMinutes(APPOINTMENT_DURATION_MINUTES);
                                continue;
                            }
                            if (IsOverlappingBlocked(current, slotEnd, mergedBlocked))
                            {
                                current = current.AddMinutes(APPOINTMENT_DURATION_MINUTES);
                                continue;
                            }
                            if (HasAppointmentConflict(current, slotEnd, dayAppointments))
                            {
                                current = current.AddMinutes(APPOINTMENT_DURATION_MINUTES);
                                continue;
                            }
                            results.Add(current);
                            current = current.AddMinutes(APPOINTMENT_DURATION_MINUTES);
                        }
                    }
                    dayToCheck = dayToCheck.AddDays(1);
                }
                return results;
            }, TimeSpan.FromHours(24));

            return result;
        }

        public async Task<List<DoctorScheduleRule>> GetAllScheduleRulesForDoctorAsync(int doctorId)
        {
            return await _context.DoctorScheduleRules.Where(r => r.DoctorId == doctorId).ToListAsync();
        }

        public async Task<DoctorFullCalendarDto> GetDoctorFullCalendarAsync(int doctorId, DateTime rangeStart, DateTime rangeEnd)
        {
            if ((rangeEnd - rangeStart).TotalDays > 14)
            {
                _notificationContext.AddNotification("AvailabilityRangeTooLong", "O período de consulta não pode ser maior que 14 dias.");
                return null;
            }

            List<CalendarEventDto> events = new List<CalendarEventDto>();

            // Percorre cada dia do intervalo
            for (var day = rangeStart.Date; day <= rangeEnd.Date; day = day.AddDays(1))
            {
                // Recupera os intervalos disponíveis para o dia
                var availableIntervals = await GetMergedAvailableIntervalsForDayAsync(doctorId, day);
                if (availableIntervals == null || availableIntervals.Count == 0)
                {
                    // Se não há disponibilidade definida para o dia, você pode optar por ignorá-lo
                    continue;
                }

                // Recupera os intervalos bloqueados para o dia
                var blockedIntervals = await GetMergedBlockedIntervalsForDayAsync(doctorId, day);

                // Recupera os agendamentos para o dia (considera agendamentos com status Scheduled ou Accepted)
                var dayAppointments = await _context.Appointments
                    .Where(a => a.DoctorId == doctorId &&
                                a.StartDateTime.Date == day &&
                                (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Accepted))
                    .ToListAsync();

                // Para cada intervalo de disponibilidade do dia, vamos dividir em segmentos
                foreach (var available in availableIntervals)
                {
                    DateTime aStart = available.Start;
                    DateTime aEnd = available.End;

                    // Lista que conterá as fronteiras (inícios e fins) do intervalo
                    List<DateTime> boundaries = new List<DateTime> { aStart, aEnd };

                    // Adiciona as fronteiras dos intervalos bloqueados que se sobrepõem ao intervalo disponível
                    foreach (var blocked in blockedIntervals)
                    {
                        // Calcula a sobreposição entre o bloqueio e o intervalo disponível
                        DateTime overlapStart = blocked.Start < aStart ? aStart : blocked.Start;
                        DateTime overlapEnd = blocked.End > aEnd ? aEnd : blocked.End;
                        if (overlapStart < overlapEnd)
                        {
                            boundaries.Add(overlapStart);
                            boundaries.Add(overlapEnd);
                        }
                    }

                    // Adiciona as fronteiras dos agendamentos que se sobrepõem ao intervalo disponível
                    foreach (var appt in dayAppointments)
                    {
                        DateTime apptStart = appt.StartDateTime;
                        DateTime apptEnd = appt.StartDateTime.AddMinutes(APPOINTMENT_DURATION_MINUTES);
                        if (apptStart < aEnd && apptEnd > aStart)
                        {
                            // Ajusta para que os limites não ultrapassem o intervalo disponível
                            DateTime segStart = apptStart < aStart ? aStart : apptStart;
                            DateTime segEnd = apptEnd > aEnd ? aEnd : apptEnd;
                            boundaries.Add(segStart);
                            boundaries.Add(segEnd);
                        }
                    }

                    // Remove duplicatas e ordena as fronteiras
                    boundaries = boundaries.Distinct().OrderBy(b => b).ToList();

                    // Cria os segmentos entre cada par de fronteiras
                    for (int i = 0; i < boundaries.Count - 1; i++)
                    {
                        DateTime segStart = boundaries[i];
                        DateTime segEnd = boundaries[i + 1];
                        if (segStart >= segEnd) continue;

                        string segmentType = "Available";
                        string description = "Horário disponível";

                        // Verifica se o segmento corresponde a um agendamento
                        bool isAppointment = dayAppointments.Any(a =>
                            a.StartDateTime <= segStart &&
                            a.StartDateTime.AddMinutes(APPOINTMENT_DURATION_MINUTES) >= segEnd);
                        if (isAppointment)
                        {
                            segmentType = "Appointment";
                            var appt = dayAppointments.First(a =>
                                a.StartDateTime <= segStart &&
                                a.StartDateTime.AddMinutes(APPOINTMENT_DURATION_MINUTES) >= segEnd);
                            description = $"Agendamento (ID: {appt.Id})";
                        }
                        else
                        {
                            // Se não é agendamento, verifica se está dentro de um bloqueio
                            bool isBlocked = blockedIntervals.Any(b =>
                                b.Start <= segStart && b.End >= segEnd);
                            if (isBlocked)
                            {
                                segmentType = "Blocked";
                                description = "Horário bloqueado";
                            }
                        }

                        events.Add(new CalendarEventDto
                        {
                            Start = segStart,
                            End = segEnd,
                            Type = segmentType,
                            Description = description
                        });
                    }
                }
            }

            // Ordena os eventos por data/hora de início
            events = events.OrderBy(e => e.Start).ToList();

            return new DoctorFullCalendarDto
            {
                DoctorId = doctorId,
                Events = events
            };
        }

        // --------------------------
        // Helper Methods
        // --------------------------
        private DateTime AlignToThirtyMinuteBoundary(DateTime dt)
        {
            var totalMinutes = (int)dt.TimeOfDay.TotalMinutes;
            int remainder = totalMinutes % 30;
            int aligned = totalMinutes - remainder;
            return dt.Date.AddMinutes(aligned);
        }

        private async Task<bool> IsCoveredByAvailabilityOnTheFlyAsync(int doctorId, DateTime start, DateTime end)
        {
            if (start.Date != end.Date)
            {
                _notificationContext.AddNotification("NotImplemented", "Multi-day scheduling is not supported.");
                return false;
            }
            var day = start.Date;
            var availableRules = await _context.DoctorScheduleRules
                .Where(r => r.DoctorId == doctorId && r.ScheduleType == ScheduleType.Available && r.StartDate <= day && (r.EndDate == null || r.EndDate >= day))
                .ToListAsync();
            availableRules = availableRules.Where(r => RuleAppliesToDay(r, day, day.DayOfWeek)).ToList();
            if (!availableRules.Any())
                return false;
            var intervals = new List<(DateTime Start, DateTime End)>();
            foreach (var rule in availableRules)
            {
                var intervalStart = day.Add(rule.StartTimeOfDay);
                var intervalEnd = day.Add(rule.EndTimeOfDay);
                if (intervalEnd > intervalStart)
                    intervals.Add((intervalStart, intervalEnd));
            }
            if (!intervals.Any())
                return false;
            var mergedIntervals = MergeIntervals(intervals);
            foreach (var (intervalStart, intervalEnd) in mergedIntervals)
            {
                if (intervalStart <= start && intervalEnd >= end)
                    return true;
            }
            return false;
        }

        private async Task<List<(DateTime Start, DateTime End)>> GetMergedAvailableIntervalsForDayAsync(int doctorId, DateTime day)
        {
            var possibleRules = await _context.DoctorScheduleRules
                .Where(r => r.DoctorId == doctorId && r.ScheduleType == ScheduleType.Available && r.StartDate <= day && (r.EndDate == null || r.EndDate >= day))
                .ToListAsync();
            var dow = day.DayOfWeek;
            possibleRules = possibleRules.Where(r => RuleAppliesToDay(r, day, dow)).ToList();
            var intervals = new List<(DateTime Start, DateTime End)>();
            foreach (var rule in possibleRules)
            {
                var iStart = day.Add(rule.StartTimeOfDay);
                var iEnd = day.Add(rule.EndTimeOfDay);
                if (iEnd > iStart)
                    intervals.Add((iStart, iEnd));
            }
            if (!intervals.Any())
                return new List<(DateTime, DateTime)>();
            return MergeIntervals(intervals);
        }

        private async Task<List<(DateTime Start, DateTime End)>> GetMergedBlockedIntervalsForDayAsync(int doctorId, DateTime day)
        {
            var blockedRules = await _context.DoctorScheduleRules
                .Where(r => r.DoctorId == doctorId && r.ScheduleType == ScheduleType.Blocked && r.StartDate <= day && (r.EndDate == null || r.EndDate >= day))
                .ToListAsync();
            var dow = day.DayOfWeek;
            blockedRules = blockedRules.Where(r => RuleAppliesToDay(r, day, dow)).ToList();
            var intervals = new List<(DateTime Start, DateTime End)>();
            foreach (var rule in blockedRules)
            {
                var bStart = day.Add(rule.StartTimeOfDay);
                var bEnd = day.Add(rule.EndTimeOfDay);
                if (bEnd > bStart)
                    intervals.Add((bStart, bEnd));
            }
            if (!intervals.Any())
                return new List<(DateTime, DateTime)>();
            return MergeIntervals(intervals);
        }

        private List<(DateTime Start, DateTime End)> MergeIntervals(List<(DateTime Start, DateTime End)> intervals)
        {
            intervals = intervals.OrderBy(i => i.Start).ThenBy(i => i.End).ToList();
            var merged = new List<(DateTime Start, DateTime End)>();
            var (currStart, currEnd) = intervals[0];
            for (int i = 1; i < intervals.Count; i++)
            {
                var (nextStart, nextEnd) = intervals[i];
                if (nextStart <= currEnd)
                {
                    if (nextEnd > currEnd)
                        currEnd = nextEnd;
                }
                else
                {
                    merged.Add((currStart, currEnd));
                    currStart = nextStart;
                    currEnd = nextEnd;
                }
            }
            merged.Add((currStart, currEnd));
            return merged;
        }

        private bool RuleAppliesToDay(DoctorScheduleRule rule, DateTime date, DayOfWeek dayOfWeek)
        {
            if (rule.FrequencyType == FrequencyType.Daily)
                return true;
            else if (rule.FrequencyType == FrequencyType.Weekly)
            {
                if (string.IsNullOrWhiteSpace(rule.DaysOfWeek))
                    return false;
                var tokens = rule.DaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).ToList();
                var shortName = dayOfWeek.ToString().Substring(0, 3);
                return tokens.Any(t => t.Equals(shortName, StringComparison.OrdinalIgnoreCase) ||
                                       t.Equals(dayOfWeek.ToString(), StringComparison.OrdinalIgnoreCase));
            }
            return false;
        }

        private bool IsOverlappingBlocked(DateTime slotStart, DateTime slotEnd, List<(DateTime Start, DateTime End)> mergedBlocked)
        {
            foreach (var (bs, be) in mergedBlocked)
            {
                if (bs < slotEnd && be > slotStart)
                    return true;
            }
            return false;
        }

        private bool HasAppointmentConflict(DateTime slotStart, DateTime slotEnd, List<Appointment> dayAppointments)
        {
            foreach (var ap in dayAppointments)
            {
                DateTime apStart = ap.StartDateTime;
                DateTime apEnd = ap.StartDateTime.AddMinutes(APPOINTMENT_DURATION_MINUTES);
                if (apStart < slotEnd && apEnd > slotStart)
                    return true;
            }
            return false;
        }
    }
}
