using Hackathon.HealthMed.Domain.Models.Enum;
using Hackathon.HealthMed.Tests.Fixture;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Hackathon.HealthMed.Tests.Integration
{
    [Trait("Category", "Integration")]
    public class MinimalIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public MinimalIntegrationTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        #region Métodos Auxiliares de Login

        private DateTime GetNextAvailableDay()
        {
            DateTime day = DateTime.UtcNow.Date.AddDays(1);
            while (!new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }.Contains(day.DayOfWeek))
            {
                day = day.AddDays(1);
            }
            return day;
        }

        /// <summary>
        /// Realiza o login do médico utilizando os dados seed (LicenseNumber "12345" / password "password").
        /// </summary>
        private async Task<string> GetDoctorTokenAsync()
        {
            var loginRequest = new
            {
                licenseNumber = "12345",
                password = "password"
            };

            var content = new StringContent(JsonConvert.SerializeObject(loginRequest), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/auth/doctor/login", content);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            dynamic obj = JsonConvert.DeserializeObject(responseString);
            return (string)obj.token;
        }

        private async Task<int> GetDoctorIdByLicenseAsync(string licenseNumber)
        {
            // Consulta os médicos sem filtro específico para retornar os dados seed.
            var response = await _client.GetAsync("/api/doctors?minRating=0&page=1&pageSize=10");
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            dynamic result = JsonConvert.DeserializeObject(responseString);
            foreach (var doctor in result.items)
            {
                if (((string)doctor.licenseNumber).Equals(licenseNumber, StringComparison.OrdinalIgnoreCase))
                {
                    return (int)doctor.id;
                }
            }
            return -1;
        }

        /// <summary>
        /// Realiza o login do paciente utilizando os dados seed (Email "patient1@example.com", NationalId "11111111111" e password "password").
        /// </summary>
        private async Task<string> GetPatientTokenAsync()
        {
            var loginRequest = new
            {
                email = "patient1@example.com",
                nationalId = "11111111111",
                password = "password"
            };

            var content = new StringContent(JsonConvert.SerializeObject(loginRequest), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/auth/patient/login", content);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            dynamic obj = JsonConvert.DeserializeObject(responseString);
            return (string)obj.token;
        }

        #endregion

        #region Autenticação

        [Fact]
        public async Task DoctorAuthentication_ReturnsToken()
        {
            string token = await GetDoctorTokenAsync();
            Assert.False(string.IsNullOrWhiteSpace(token));
        }

        [Fact]
        public async Task PatientAuthentication_ReturnsToken()
        {
            string token = await GetPatientTokenAsync();
            Assert.False(string.IsNullOrWhiteSpace(token));
        }

        #endregion

        #region Cadastro/Edição de Horários Disponíveis (Médico)

        [Fact]
        public async Task Doctor_CanCreateAndEditScheduleRule_Valid()
        {
            // Autentica o médico.
            string token = await GetDoctorTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            DateTime startDate = DateTime.UtcNow.Date.AddDays(1);
            DateTime endDate = DateTime.UtcNow.Date.AddDays(7);
            var createRuleRequest = new
            {
                scheduleType = (int)ScheduleType.Available,
                frequencyType = (int)FrequencyType.Daily,
                startDate = startDate.ToString("yyyy-MM-dd"),
                endDate = endDate.ToString("yyyy-MM-dd"),
                startTimeOfDay = "08:00:00",
                endTimeOfDay = "17:00:00",
                daysOfWeek = ""
            };
            var createContent = new StringContent(JsonConvert.SerializeObject(createRuleRequest), Encoding.UTF8, "application/json");
            var createResponse = await _client.PostAsync("/api/doctor/schedule-rules", createContent);
            createResponse.EnsureSuccessStatusCode();
            var createResponseString = await createResponse.Content.ReadAsStringAsync();
            Assert.Contains("Rule created successfully", createResponseString);

            // Busca as regras criadas para obter o ID da regra criada.
            var getResponse = await _client.GetAsync("/api/doctor/schedule-rules");
            getResponse.EnsureSuccessStatusCode();
            var getResponseString = await getResponse.Content.ReadAsStringAsync();
            dynamic rules = JsonConvert.DeserializeObject(getResponseString);
            int ruleId = -1;
            foreach (var rule in rules)
            {
                // Converte o token para DateTime
                DateTime ruleStartDate = rule.Value<DateTime>("startDate");
                if (ruleStartDate.Date == startDate.Date)
                {
                    ruleId = rule.Value<int>("id");
                    break;
                }
            }
            Assert.True(ruleId > 0, "A regra criada não foi encontrada na listagem.");

            // Edição da regra: altera o horário de término para 18:00:00.
            var updateRuleRequest = new
            {
                scheduleType = (int)ScheduleType.Available,
                frequencyType = (int)FrequencyType.Daily,
                startDate = startDate.ToString("yyyy-MM-dd"),
                endDate = endDate.ToString("yyyy-MM-dd"),
                startTimeOfDay = "08:00:00",
                endTimeOfDay = "18:00:00",  // horário alterado
                daysOfWeek = ""
            };
            var updateContent = new StringContent(JsonConvert.SerializeObject(updateRuleRequest), Encoding.UTF8, "application/json");
            var updateResponse = await _client.PutAsync($"/api/doctor/schedule-rules/{ruleId}", updateContent);
            updateResponse.EnsureSuccessStatusCode();
            var updateResponseString = await updateResponse.Content.ReadAsStringAsync();
            Assert.Contains("Rule updated successfully", updateResponseString);
        }

        [Fact]
        public async Task Doctor_CannotCreateScheduleRule_WhenEndTimeBeforeStartTime()
        {
            // Autentica o médico.
            string token = await GetDoctorTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Tenta criar uma regra com horário de início às 17:00 e término às 09:00 (inválido).
            var invalidRuleRequest = new
            {
                scheduleType = (int)ScheduleType.Available,  // ou simplesmente 0
                frequencyType = (int)FrequencyType.Daily,      // ou simplesmente 0
                startDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd"),
                endDate = DateTime.UtcNow.Date.AddDays(7).ToString("yyyy-MM-dd"),
                startTimeOfDay = "17:00:00",
                endTimeOfDay = "09:00:00",  // inválido: término antes do início
                daysOfWeek = ""
            };

            var content = new StringContent(JsonConvert.SerializeObject(invalidRuleRequest), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/doctor/schedule-rules", content);

            // Supõe que o endpoint retorne BadRequest (400) quando há erro de validação.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Contains("InvalidTime", responseString);
        }

        #endregion

        #region Aceite ou Recusa de Consultas Médicas (Médico)

        [Fact]
        public async Task Doctor_CanAcceptOrRejectAppointment()
        {
            // Obtém o ID do médico a partir do licenseNumber seed ("12345").
            int doctorId = await GetDoctorIdByLicenseAsync("12345");
            Assert.True(doctorId > 0, "Médico com licenseNumber '12345' não foi encontrado.");

            // Seleciona um dia disponível (próximo dia que seja Mon, Wed ou Fri).
            DateTime availableDay = GetNextAvailableDay();
            // Define dois horários válidos dentro do intervalo seed (08:00 às 12:00).
            DateTime appointmentTime1 = availableDay.AddHours(9);  // 09:00
            DateTime appointmentTime2 = availableDay.AddHours(10); // 10:00

            // O paciente agenda a primeira consulta.
            string patientToken = await GetPatientTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", patientToken);

            var appointmentRequest = new
            {
                doctorId = doctorId,
                timeSlot = appointmentTime1.ToString("o"),
                uniqueRequestId = Guid.NewGuid().ToString()
            };
            var appointmentContent = new StringContent(JsonConvert.SerializeObject(appointmentRequest), Encoding.UTF8, "application/json");
            var appointmentResponse = await _client.PostAsync("/api/appointments", appointmentContent);
            appointmentResponse.EnsureSuccessStatusCode();
            var appointmentResponseString = await appointmentResponse.Content.ReadAsStringAsync();
            dynamic appointmentObj = JsonConvert.DeserializeObject(appointmentResponseString);
            int appointmentId = (int)appointmentObj.id;

            // O médico aceita a primeira consulta.
            string doctorToken = await GetDoctorTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", doctorToken);

            var acceptResponse = await _client.PostAsync($"/api/doctor/appointments/{appointmentId}/accept", null);
            acceptResponse.EnsureSuccessStatusCode();
            var acceptResponseString = await acceptResponse.Content.ReadAsStringAsync();
            Assert.Contains("\"status\":1", acceptResponseString);

            // O paciente agenda uma segunda consulta.
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", patientToken);
            var appointmentRequest2 = new
            {
                doctorId = doctorId,
                timeSlot = appointmentTime2.ToString("o"),
                uniqueRequestId = Guid.NewGuid().ToString()
            };
            var appointmentContent2 = new StringContent(JsonConvert.SerializeObject(appointmentRequest2), Encoding.UTF8, "application/json");
            var appointmentResponse2 = await _client.PostAsync("/api/appointments", appointmentContent2);
            appointmentResponse2.EnsureSuccessStatusCode();
            var appointmentResponseString2 = await appointmentResponse2.Content.ReadAsStringAsync();
            dynamic appointmentObj2 = JsonConvert.DeserializeObject(appointmentResponseString2);
            int appointmentId2 = (int)appointmentObj2.id;

            // O médico rejeita a segunda consulta.
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", doctorToken);
            var rejectResponse = await _client.PostAsync($"/api/doctor/appointments/{appointmentId2}/reject", null);
            rejectResponse.EnsureSuccessStatusCode();
            var rejectResponseString = await rejectResponse.Content.ReadAsStringAsync();
            Assert.Contains("\"status\":2", rejectResponseString);
        }

        #endregion

        #region Busca por Médicos (Paciente)

        [Fact]
        public async Task Patient_CanSearchForDoctors()
        {
            // O endpoint de busca é público.
            string query = "/api/doctors?specialty=Cardiology&minRating=3&page=1&pageSize=5";
            var response = await _client.GetAsync(query);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Contains("items", responseString, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Agendamento de Consultas (Paciente)

        [Fact]
        public async Task Patient_CanScheduleAppointment()
        {
            // Autentica o paciente.
            string token = await GetPatientTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            DateTime availableDay = GetNextAvailableDay();
            DateTime appointmentTime = availableDay.AddHours(9); // horário válido, entre 08:00 e 12:00

            var appointmentRequest = new
            {
                doctorId = 1,
                timeSlot = appointmentTime.ToString("o"),
                uniqueRequestId = Guid.NewGuid().ToString()
            };
            var content = new StringContent(JsonConvert.SerializeObject(appointmentRequest), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/appointments", content);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Contains("id", responseString, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
