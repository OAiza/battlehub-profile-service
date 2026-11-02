-- Generación de datos para docker compose (R-28).
--
-- Lo ejecuta el servicio "generar-datos" de docker-compose.yml después de aplicar las
-- migraciones. Es independiente del proyecto: no forma parte de Program.cs, de las
-- migraciones ni del seed (el seed solo carga el catálogo de juegos y los permisos).
--
-- Inserta perfiles de ejemplo para que, tras "docker compose up", la base tenga datos.
-- Se puede ejecutar varias veces: INSERT IGNORE no duplica perfiles que ya existen.
-- Los identificadores siguen el formato del claim "sub" de Auth0, pero son ficticios:
-- no corresponden a cuentas reales.
--
-- Si cambia el modelo de datos, este script se actualiza en el mismo PR.

INSERT IGNORE INTO profiles (id, display_name, email, created_at, last_login_at) VALUES
    ('auth0|ejemplo-ana',      'Ana',      'ana@ejemplo.com',      UTC_TIMESTAMP(6) - INTERVAL 30 DAY, UTC_TIMESTAMP(6) - INTERVAL 1 DAY),
    ('auth0|ejemplo-bruno',    'Bruno',    'bruno@ejemplo.com',    UTC_TIMESTAMP(6) - INTERVAL 25 DAY, UTC_TIMESTAMP(6) - INTERVAL 2 DAY),
    ('auth0|ejemplo-carla',    'Carla',    NULL,                   UTC_TIMESTAMP(6) - INTERVAL 20 DAY, UTC_TIMESTAMP(6) - INTERVAL 3 DAY),
    ('auth0|ejemplo-diego',    'Diego',    'diego@ejemplo.com',    UTC_TIMESTAMP(6) - INTERVAL 15 DAY, UTC_TIMESTAMP(6) - INTERVAL 4 DAY),
    ('auth0|ejemplo-elena',    'Elena',    NULL,                   UTC_TIMESTAMP(6) - INTERVAL 10 DAY, UTC_TIMESTAMP(6) - INTERVAL 5 DAY),
    ('google-oauth2|ejemplo-fer', 'Fernando', 'fernando@ejemplo.com', UTC_TIMESTAMP(6) - INTERVAL 5 DAY, UTC_TIMESTAMP(6));

SELECT COUNT(*) AS perfiles_en_la_base FROM profiles;
