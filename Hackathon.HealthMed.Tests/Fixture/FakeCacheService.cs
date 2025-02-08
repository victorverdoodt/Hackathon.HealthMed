using Hackathon.HealthMed.Domain.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hackathon.HealthMed.Tests.Fixture
{
    public class FakeCacheService : ICacheService
    {

        private readonly Dictionary<string, object> _cache = new Dictionary<string, object>();

        public async Task<T> GetOrAddAsync<T>(string cacheKey, Func<Task<T>> factory, TimeSpan cacheDuration)
        {

            if (_cache.TryGetValue(cacheKey, out var cachedValue))
            {
                return (T)cachedValue;
            }


            T result = await factory();

            _cache[cacheKey] = result;
            return result;
        }

        public Task InvalidateCacheByKeyAsync(string key)
        {

            _cache.Remove(key);
            return Task.CompletedTask;
        }
    }
}
