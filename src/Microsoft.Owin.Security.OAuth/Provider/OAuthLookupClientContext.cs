// <copyright file="OAuthLookupClientContext.cs" company="Microsoft Open Technologies, Inc.">
// Copyright 2011-2013 Microsoft Open Technologies, Inc. All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

#if AUTHSERVER

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Owin.Security.Provider;

namespace Microsoft.Owin.Security.OAuth
{
    public class ClientDetails
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        [SuppressMessage("Microsoft.Design", "CA1056:UriPropertiesShouldNotBeStrings", Justification = "To be compared to querystring value")]
        public string RedirectUri { get; set; }
    }

    public class OAuthLookupClientContext : BaseContext
    {
        public OAuthLookupClientContext(
            IOwinContext context,
            ClientDetails requestDetails)
            : base(context)
        {
            RequestDetails = requestDetails;
        }

        public ClientDetails RequestDetails { get; private set; }

        public string ClientId
        {
            get { return RequestDetails.ClientId; }
        }

        public ClientDetails FoundDetails { get; private set; }

        public bool IsValidated { get; private set; }

        public void ClientFound(ClientDetails foundDetails)
        {
            FoundDetails = foundDetails;

            if (RequestDetails.ClientId != null &&
                !String.Equals(RequestDetails.ClientId, FoundDetails.ClientId, StringComparison.Ordinal))
            {
                return;
            }

            if (RequestDetails.ClientSecret != null &&
                !String.Equals(RequestDetails.ClientSecret, FoundDetails.ClientSecret, StringComparison.Ordinal))
            {
                return;
            }

            if (RequestDetails.RedirectUri != null &&
                !String.Equals(RequestDetails.RedirectUri, FoundDetails.RedirectUri, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            IsValidated = true;
        }
    }
}

#endif