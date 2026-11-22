-- Runs automatically on first container boot (postgres-user ONLY).
-- orderservice_db and paymentservice_db are NO LONGER created here — they now
-- live in their own dedicated containers (postgres-order, postgres-payment),
-- each creating its database directly via its own POSTGRES_DB env var, exactly
-- like postgres-user already does. This keeps the "one DB container per
-- service" rule (see docker-compose.yml header comment) consistent instead of
-- special-casing Order/Payment as extra databases inside postgres-user.

CREATE DATABASE userservice_db;

-- Grant the shared user access
GRANT ALL PRIVILEGES ON DATABASE userservice_db TO ecommerce;
