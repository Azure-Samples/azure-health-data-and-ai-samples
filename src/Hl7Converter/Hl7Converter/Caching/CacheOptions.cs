namespace HL7Converter.Caching
{
    internal class CacheOptions
    {
        /// <summary>
        /// Gets or sets the expiration time of a cached item.
        /// </summary>
        public TimeSpan CacheItemExpiry { get; set; }
    }
}
