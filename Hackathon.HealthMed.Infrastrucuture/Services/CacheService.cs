using Hackathon.HealthMed.Domain.Models.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Hackathon.HealthMed.Infrastrucuture.Services
{
    public class CacheService : ICacheService
    {
        private readonly IDistributedCache _distributedCache;

        public CacheService(IDistributedCache distributedCache)
        {
            _distributedCache = distributedCache;
        }

        public async Task<T> GetOrAddAsync<T>(string cacheKey, Func<Task<T>> factory, TimeSpan cacheDuration)
        {
            var cacheData = await _distributedCache.GetStringAsync(cacheKey);
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            if (!string.IsNullOrEmpty(cacheData))
            {
                return JsonSerializer.Deserialize<T>(cacheData, jsonOptions);
            }

            var data = await factory();

            if (data != null)
            {
                string serializedData = JsonSerializer.Serialize(data, data.GetType(), jsonOptions);

                await _distributedCache.SetStringAsync(cacheKey, serializedData, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = cacheDuration
                });
            }

            return data;
        }

        public async Task InvalidateCacheByKeyAsync(string key)
        {
            await _distributedCache.RemoveAsync(key);
        }
    }
}
