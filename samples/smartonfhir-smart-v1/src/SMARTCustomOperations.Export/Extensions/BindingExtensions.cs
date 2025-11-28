// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.AzureHealth.DataServices.Bindings;
using Microsoft.AzureHealth.DataServices.Pipelines;
using Newtonsoft.Json;

namespace SMARTCustomOperations.AzureAuth.Extensions
{
    public static class BindingExtensions
    {
        public static OperationContext SetContextErrorBody(this OperationContext context, BindingErrorEventArgs args, bool debug = false)
        {
            if (!debug)
            {
                args = new BindingErrorEventArgs(args.Name, args.Id, args.Error);
            }

            // System.Text.Json does not support int pointers (like HTTP status codes).
            context.ContentString = JsonConvert.SerializeObject(args);
            return context;
        }
    }
}
