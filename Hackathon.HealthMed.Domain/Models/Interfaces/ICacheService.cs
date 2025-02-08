namespace Hackathon.HealthMed.Domain.Models.Interfaces
{
    public interface ICacheService
    {
        Task<T> GetOrAddAsync<T>(string cacheKey, Func<Task<T>> factory, TimeSpan cacheDuration);

        Task InvalidateCacheByKeyAsync(string key);
    }
}
