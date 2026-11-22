# Copilot Instructions

## Project Guidelines
- In UserService, keep modular DI extension structure (ServiceCollectionExtensions, Database, Kafka, Security, Redis, Email, Migration extensions) and avoid adding a monolithic duplicate InfrastructureServiceExtensions with overlapping AddInfrastructure/ApplyMigrationsAsync methods.