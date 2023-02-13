namespace HL7Converter.Caching
{
    public interface IAuthTokenCache
    {
        /// <summary>
        /// Add Token value to cache.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task AddToken(string key, string value);

        /// <summary>
        /// Get the token from the cache.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<string> GetToken(string key);

    }
}
