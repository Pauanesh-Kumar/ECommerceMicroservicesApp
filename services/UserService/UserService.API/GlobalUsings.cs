// Central place for namespace aliases needed because HotChocolate/GreenDonut
// define their own Result<T> and Error types that collide with our Domain
// layer's names. Declared once here (global using) instead of repeating
// per-file — any file in this project can use DomainCommon.Result,
// DomainCommon.Result<T>, and DomainErrors.Error unambiguously.
global using DomainCommon = UserService.Domain.Common;
global using DomainErrors = UserService.Domain.Errors;
