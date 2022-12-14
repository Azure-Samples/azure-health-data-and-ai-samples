namespace UploadFhirJson.Caching
{
    public interface IInMemoryCache
    {
        /// <summary>
        /// Adds an object to the cache.
        /// </summary>
        /// <typeparam name="T">Type of object.</typeparam>
        /// <param name="key">Cache key.</param>
        /// <param name="value">Item to cache.</param>
        /// <returns>Task.</returns>
        Task AddAsync(string key, string value);

        /// <summary>
        /// Gets an item from the cache.
        /// </summary>
        /// <typeparam name="T">Type of item to return from cache.</typeparam>
        /// <param name="key">Cache key.</param>
        /// <returns></returns>
        Task<string> GetAsync(string key);

        /// <summary>
        /// Removes an item from the cache.
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <returns>True if removed otherwise false.</returns>
        Task<bool> RemoveAsync(string key);

    }
}
