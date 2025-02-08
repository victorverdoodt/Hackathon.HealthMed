using Hackathon.HealthMed.Infrastrucuture.Resources;
using Hackathon.HealthMed.Infrastrucuture.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;
using System.Text.Json;

namespace Hackathon.HealthMed.Infrastrucuture.Filters
{
    public class NotificationFilter : IAsyncResultFilter
    {
        private readonly NotificationContextService _notificationContext;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public NotificationFilter(NotificationContextService notificationContext, IStringLocalizer<SharedResource> localizer)
        {
            _notificationContext = notificationContext;
            _localizer = localizer;
        }

        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            // Verificar se há notificações
            if (_notificationContext.HasNotifications)
            {
                // Substituir a resposta atual com os erros
                var errorResponse = new
                {
                    errors = _notificationContext.Notifications.Select(n => new
                    {
                        code = n.Key,
                        message = string.IsNullOrEmpty(n.Message) ? _localizer[n.Key].Value : n.Message
                    })
                };

                var jsonResponse = JsonSerializer.Serialize(errorResponse);

                context.Result = new ContentResult
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    ContentType = "application/json",
                    Content = jsonResponse
                };

                _notificationContext.Clear(); // Limpar notificações após processar
            }

            // Continuar para o próximo filtro (se necessário)
            await next();
        }
    }
}

