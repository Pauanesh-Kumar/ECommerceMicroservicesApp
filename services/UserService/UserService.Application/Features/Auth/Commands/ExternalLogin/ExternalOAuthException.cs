using AutoMapper.Features;

using System;

namespace UserService.Application.Features.Auth.Commands.ExternalLogin;

public sealed class ExternalOAuthException : Exception
{
    public ExternalOAuthException(string message) : base(message) { }
    public ExternalOAuthException(string message, Exception inner) : base(message, inner) { }
}