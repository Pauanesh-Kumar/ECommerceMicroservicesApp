-- Runs automatically on first container boot
-- Creates separate databases for each service that uses PostgreSQL

CREATE DATABASE userservice_db;
CREATE DATABASE orderservice_db;
CREATE DATABASE paymentservice_db;

-- Grant the shared user access to each DB
GRANT ALL PRIVILEGES ON DATABASE userservice_db   TO ecommerce;
GRANT ALL PRIVILEGES ON DATABASE orderservice_db  TO ecommerce;
GRANT ALL PRIVILEGES ON DATABASE paymentservice_db TO ecommerce;
