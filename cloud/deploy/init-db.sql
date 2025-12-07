-- LRM Cloud Database Initialization
-- This script runs on first PostgreSQL container start

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Grant privileges (database and user created by POSTGRES_DB/POSTGRES_USER env vars)
-- Additional setup can be added here as needed
