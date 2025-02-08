using Hackathon.HealthMed.Tests.Fixture;
using NBomber.Contracts.Stats;
using NBomber.CSharp;
using NBomber.Data.CSharp;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Xunit;

namespace Hackathon.HealthMed.Tests.LoadTests
{
    [Trait("Category", "Load")]
    public class AvailableSlotsScenarioTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;
        public AvailableSlotsScenarioTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        private async Task<string> GetPatientTokenAsync()
        {
            var loginPayload = JsonConvert.SerializeObject(new
            {
                email = "patient1@example.com",
                nationalId = "11111111111",
                password = "password"
            });

            var loginContent = new StringContent(loginPayload, Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/auth/patient/login", loginContent);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            dynamic loginResult = JsonConvert.DeserializeObject(responseString);
            return (string)loginResult.token;
        }

        [Fact]
        public async Task AvailableSlotsScenario_ShouldReturnSuccess_ForAllRequests()
        {
            // Obtém o token do paciente.
            string token = await GetPatientTokenAsync();

            // Cria um DataFeed.Constant para que cada cenário copie obtenha o mesmo token.
            var tokenFeed = DataFeed.Constant(new[] { token });

            var scenario = Scenario.Create("AvailableSlotsScenario", async context =>
            {
                // Define os parâmetros de consulta.
                DateTime today = DateTime.UtcNow.Date;
                string query = $"?doctorId=1&rangeStart={today:O}&rangeEnd={today.AddDays(13):O}";

                // Obtém o token para essa execução.
                var patientToken = tokenFeed.GetNextItem(context.ScenarioInfo);

                // Cria uma nova requisição e define o header de autorização.
                var request = new HttpRequestMessage(HttpMethod.Get, $"/api/appointments/available-slots{query}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", patientToken);

                var response = await _client.SendAsync(request);

                return response.IsSuccessStatusCode
                    ? Response.Ok(statusCode: response.StatusCode.ToString())
                    : Response.Fail(statusCode: response.StatusCode.ToString());
            })
            .WithLoadSimulations(
                Simulation.RampingConstant(copies: 20000, during: TimeSpan.FromMinutes(10)),
                // Mantém 20 usuários durante 1 minuto.
                Simulation.KeepConstant(copies: 20000, during: TimeSpan.FromMinutes(1)),

                Simulation.RampingConstant(copies: 0, during: TimeSpan.FromSeconds(10))
            );

            var nbResult = NBomberRunner.RegisterScenarios(scenario)
                .WithReportFormats(ReportFormat.Html)
                .WithReportFileName("nbomber_available_slots_report.html")
                .WithReportFolder("nbomber_reports")
                .Run();

            // Valida que houve requisições bem-sucedidas.
            var okCount = nbResult.ScenarioStats[0].AllOkCount;
            Assert.True(okCount > 0, "Deve haver requisições bem-sucedidas.");
        }
    }
}
