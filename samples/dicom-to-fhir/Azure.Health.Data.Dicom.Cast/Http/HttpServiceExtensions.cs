// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using FellowOakDicom.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Azure.Health.Data.Dicom.Cast.Http;
internal static class HttpServiceExtensions
{
    public static IServiceCollection AddJsonSerialization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.Configure<JsonSerializerOptions>(
            options =>
            {
                options.Converters.Add(new DicomJsonConverter());
                options.PropertyNameCaseInsensitive = true;
                options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });
    }
}
