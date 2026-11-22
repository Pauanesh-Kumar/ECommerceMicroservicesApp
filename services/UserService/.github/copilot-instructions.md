# Copilot Instructions

## Project Guidelines
- User prefers not to add a design-time IDesignTimeDbContextFactory and instead use appsettings/.env/docker-compose connection strings for EF migrations.

## Naming Conventions
- Use 'OAuth' naming for external auth interfaces and classes: IExternalOAuthService, ExternalOAuthService, IExternalOAuthProvider, ExternalOAuthException, ExternalOAuthUserInfo.