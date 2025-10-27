using Microsoft.AzureHealth.DataServices.Caching.StorageProviders;
using StackExchange.Redis;  // or your Redis client

public class RedisCacheBackingStoreWithExpiry : ICacheBackingStoreProvider
{
    private readonly ICacheBackingStoreProvider _innerProvider;
    private readonly IDatabase _redisDb;
    private readonly TimeSpan _expiry;

    public RedisCacheBackingStoreWithExpiry(ICacheBackingStoreProvider innerProvider, IConnectionMultiplexer redisConnection, TimeSpan expiry)
    {
        _innerProvider = innerProvider;
        _redisDb = redisConnection.GetDatabase();
        _expiry = expiry;
    }

    public async Task AddAsync<T>(string key, T value)
    {
        await _innerProvider.AddAsync(key, value);

        await _redisDb.KeyExpireAsync(key, _expiry);
    }

    public Task AddAsync(string key, object value)
    {
        throw new NotImplementedException();
    }

    public async Task<T> GetAsync<T>(string key)
    {
        return await _innerProvider.GetAsync<T>(key);
    }

    public async Task<string> GetAsync(string key)
    {
        return await _innerProvider.GetAsync(key);
    }

    public async Task<bool> RemoveAsync(string key)
    {
        return await _innerProvider.RemoveAsync(key);
    }
}
