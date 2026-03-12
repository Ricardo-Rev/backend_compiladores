-- ============================================================
--  SCRIPT DE BASE DE DATOS
--  Sistema de Autenticación - UMG Rover Backend
--  Base de datos: SQL Server
--
--  TABLAS QUE CREA ESTE SCRIPT:
--  1. users    → Usuarios del sistema
--  2. sesiones → Sesiones JWT activas (para revocación)
--
--  CÓMO EJECUTAR:
--  1. Abrir SQL Server Management Studio (SSMS)
--  2. Conectar a tu instancia de SQL Server
--  3. Abrir este archivo y ejecutar con F5
--
--  NOTA: Si las tablas ya existen, el script no las vuelve a crear.
-- ============================================================

-- Seleccionar la base de datos
USE umg_basic_rover;
GO

-- ============================================================
--  TABLA: users
--  Almacena los usuarios registrados en el sistema.
-- ============================================================

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'users')
BEGIN
    CREATE TABLE users (
        -- Identificador único (GUID generado por el código)
        id               UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWID(),

        -- Nombre completo del usuario
        name             NVARCHAR(150)       NOT NULL,

        -- Email único (se usa para el login)
        email            NVARCHAR(200)       NOT NULL,

        -- Hash BCrypt de la contraseña (NUNCA texto plano)
        -- BCrypt genera hashes de 60 caracteres, reservamos 255 por seguridad
        password_hash    NVARCHAR(255)       NOT NULL,

        -- Estado de la cuenta (1=activo, 0=bloqueado)
        activo           BIT                 NOT NULL DEFAULT 1,

        -- Fecha de creación de la cuenta (UTC)
        fecha_creacion   DATETIME2           NOT NULL DEFAULT GETUTCDATE(),

        -- Clave primaria
        CONSTRAINT PK_users PRIMARY KEY (id)
    );

    -- Índice único en email para búsquedas rápidas de login
    -- y para evitar emails duplicados
    CREATE UNIQUE INDEX idx_users_email
        ON users (email);

    PRINT '✅ Tabla [users] creada exitosamente.';
END
ELSE
BEGIN
    PRINT '⚠️  Tabla [users] ya existe. No se modificó.';
END
GO

-- ============================================================
--  TABLA: sesiones
--  Registra cada inicio de sesión activo.
--  Permite revocar tokens JWT cuando el usuario hace logout.
--
--  FLUJO DE SEGURIDAD:
--  1. Login exitoso → INSERT en sesiones (activa = 1)
--  2. Cada request → Middleware verifica que el hash del token
--                    exista en sesiones con activa = 1
--  3. Logout → UPDATE sesiones SET activa = 0
--  4. Cualquier request posterior → rechazado con 401
-- ============================================================

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'sesiones')
BEGIN
    CREATE TABLE sesiones (
        -- Identificador único de la sesión
        id                   UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWID(),

        -- FK al usuario dueño de la sesión
        usuario_id           UNIQUEIDENTIFIER    NOT NULL,

        -- Hash SHA-256 del token JWT
        -- NUNCA se guarda el token en texto plano
        session_token_hash   NVARCHAR(255)       NOT NULL,

        -- Fecha y hora del inicio de sesión (UTC)
        fecha_login          DATETIME2           NOT NULL DEFAULT GETUTCDATE(),

        -- Estado: 1=activa, 0=cerrada (logout)
        activa               BIT                 NOT NULL DEFAULT 1,

        -- Clave primaria
        CONSTRAINT PK_sesiones PRIMARY KEY (id),

        -- Clave foránea al usuario
        CONSTRAINT FK_sesiones_users
            FOREIGN KEY (usuario_id)
            REFERENCES users (id)
            ON DELETE CASCADE  -- Si se borra el usuario, se borran sus sesiones
    );

    -- Índice único en el hash del token para búsquedas rápidas
    -- en el middleware de revocación (se ejecuta en CADA request autenticado)
    CREATE UNIQUE INDEX idx_sesion_hash
        ON sesiones (session_token_hash);

    -- Índice compuesto para filtrar sesiones activas por usuario
    CREATE INDEX idx_sesion_usuario_activa
        ON sesiones (usuario_id, activa);

    PRINT '✅ Tabla [sesiones] creada exitosamente.';
END
ELSE
BEGIN
    PRINT '⚠️  Tabla [sesiones] ya existe. No se modificó.';
END
GO

-- ============================================================
--  VERIFICACIÓN FINAL
-- ============================================================

PRINT '';
PRINT '============================================================';
PRINT '  RESUMEN DE TABLAS EN LA BASE DE DATOS:';
PRINT '============================================================';

SELECT
    TABLE_NAME          AS 'Tabla',
    TABLE_TYPE          AS 'Tipo'
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME;

PRINT '';
PRINT '✅ Script ejecutado correctamente.';
GO
