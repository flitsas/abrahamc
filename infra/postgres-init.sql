-- Inicialización de la base de datos FLIT
-- Este script se ejecuta al crear el contenedor por primera vez

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";  -- Para búsquedas ILIKE eficientes
