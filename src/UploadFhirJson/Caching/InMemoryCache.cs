using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace UploadFhirJson.Caching
{
    public class InMemoryCache : IInMemoryCache
    {
        public InMemoryCache(IMemoryCache memoryCache)
        {
            expiry = TimeSpan.FromHours(1.0);
            this.cache = memoryCache;
        }

        private readonly IMemoryCache cache;
        private readonly ILogger logger;
        private readonly TimeSpan expiry;

        /// <summary>
        /// Adds an item to the cache.
        /// </summary>
        /// <typeparam name="T">Type of item to add.</typeparam>
        /// <param name="key">Cache key.</param>
        /// <param name="value">Item to add to cache.</param>
        /// <returns>Task.</returns>
        public async Task AddAsync(string key, string value)
        {
            string json = JsonConvert.SerializeObject(value);
            cache.Set(key, json, GetOptions());
            logger?.LogTrace("Key {key} set to local memory cache.", key);
        }

        /// <summary>
        /// Gets an item from the cache.
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <returns>An item from cache otherwise null.</returns>
        public async Task<string> GetAsync(string key)
        {
            if (cache.TryGetValue(key, out string value))
            {
                return value;
            }
            return null;
        }

        /// <summary>
        /// Removes an item from the cache and persistence provider.
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <returns>True is remove otherwise false.</returns>
        public async Task<bool> RemoveAsync(string key)
        {
            cache.Remove(key);
            return true;
        }

        private MemoryCacheEntryOptions GetOptions()
        {
            MemoryCacheEntryOptions options = new()
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(expiry.TotalMilliseconds)
            };

            _ = options.RegisterPostEvictionCallback(OnPostEviction);

            return options;
        }

        private void OnPostEviction(object key, object letter, EvictionReason reason, object state)
        {
            logger?.LogTrace("Key {key} evicted from cache.", key);
        }


    }
}
