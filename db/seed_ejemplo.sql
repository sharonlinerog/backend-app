-- Datos de ejemplo, opcionales, solo para probar el módulo de consulta con algo visible.
INSERT INTO creditos (nombre_cliente, cedula, valor_credito, tasa_interes, plazo_meses, comercial, fecha_registro)
VALUES
    ('Ana María Gómez', '1020304050', 5000000.00, 1.90, 12, 'Carlos Pérez', now() - interval '3 days'),
    ('Luis Fernando Rojas', '80123456', 12000000.00, 2.10, 24, 'Marta Suárez', now() - interval '1 day'),
    ('Diana Marcela Torres', '1013579246', 3000000.00, 1.75, 6, 'Carlos Pérez', now());
