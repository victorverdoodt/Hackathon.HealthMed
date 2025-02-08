using Hackathon.HealthMed.Domain.Models.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hackathon.HealthMed.Infrastrucuture.Services
{
    public class NotificationContextService
    {
        private readonly List<Notification> _notifications = new List<Notification>();

        public IReadOnlyCollection<Notification> Notifications => _notifications;

        public bool HasNotifications => _notifications.Any();

        public void AddNotification(string key, string message)
        {
            _notifications.Add(new Notification { Key = key, Message = message });
        }

        public void AddNotification(string key)
        {
            _notifications.Add(new Notification { Key = key, Message = null });
        }

        public void AddNotification(Notification notification)
        {
            _notifications.Add(notification);
        }

        public void AddNotifications(IEnumerable<Notification> notifications)
        {
            _notifications.AddRange(notifications);
        }

        public void Clear() => _notifications.Clear();
    }
}
