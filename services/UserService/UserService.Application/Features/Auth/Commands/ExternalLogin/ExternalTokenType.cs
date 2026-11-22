using System;
using System.Collections.Generic;
using System.Text;

namespace UserService.Application.Features.Auth.Commands.ExternalLogin
{
    public enum ExternalTokenType
    {
        /// <summary>
        /// JWT ID Token directly from provider (Google, etc.)
        /// </summary>
        IdToken = 0,

        /// <summary>
        /// Authorization code that must be exchanged for tokens
        /// </summary>
        AuthorizationCode = 1
    }
}
