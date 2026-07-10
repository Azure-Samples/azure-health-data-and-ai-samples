// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Google.Protobuf.WellKnownTypes;
using System.Text;

namespace SMARTCustomOperations.AzureAuth.Extensions
{
    public static class Base64Extensions
    {
        public static string EncodeBase64(this string value)
        {
            var valueBytes = Encoding.UTF8.GetBytes(value);
            return Convert.ToBase64String(valueBytes);
        }

        public static string? DecodeBase64(this string? value)
        {
            try
            {
                var valueBytes = Convert.FromBase64String(value ?? "");
                return Encoding.UTF8.GetString(valueBytes);
            }
            catch (Exception) { }

            try
            {
                var valueBytes = Convert.FromBase64String((value ?? "") + "=");
                return Encoding.UTF8.GetString(valueBytes);
            }
            catch (Exception) { }

            return null;
        }
    }
}
