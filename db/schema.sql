-- =========================================================================
-- Script de creación de la base de datos para la App de Créditos
-- Motor: PostgreSQL 14+
--
-- Uso:
--   1. Crear la base y el usuario (ajusta la clave):
--        CREATE USER creditos_app WITH PASSWORD 'una_clave_segura';
--        CREATE DATABASE creditosdb OWNER creditos_app;
--   2. Conectarte a creditosdb y ejecutar este archivo:
--        psql -U creditos_app -d creditosdb -f schema.sql
-- =========================================================================

-- Necesario para poder usar gen_random_uuid() como valor por defecto del id
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS creditos (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    nombre_cliente  VARCHAR(150)     NOT NULL,
    cedula          VARCHAR(30)      NOT NULL,
    valor_credito   NUMERIC(18,2)    NOT NULL CHECK (valor_credito > 0),
    tasa_interes    NUMERIC(5,2)     NOT NULL CHECK (tasa_interes >= 0),
    plazo_meses     INTEGER          NOT NULL CHECK (plazo_meses > 0),
    comercial       VARCHAR(150)     NOT NULL,
    fecha_registro  TIMESTAMPTZ      NOT NULL DEFAULT now()
);

-- Índices para que los filtros y el ordenamiento del módulo de consulta sean rápidos
CREATE INDEX IF NOT EXISTS ix_creditos_nombre_cliente ON creditos (nombre_cliente);
CREATE INDEX IF NOT EXISTS ix_creditos_cedula          ON creditos (cedula);
CREATE INDEX IF NOT EXISTS ix_creditos_comercial        ON creditos (comercial);
CREATE INDEX IF NOT EXISTS ix_creditos_fecha_registro   ON creditos (fecha_registro);
