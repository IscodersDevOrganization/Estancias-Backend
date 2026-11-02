/* =====================================================================
   BORRAR TODO LO GENERADO DE UN PERÍODO (FORZADO)
   ---------------------------------------------------------------------
   Igual que ReiniciarGeneracionResumen.sql, pero borra AUNQUE ya se hayan
   enviado mails a clientes: se pierde el historial de esos envíos y, al
   regenerar, esos clientes quedan de nuevo en la cola de envío.
   Ejecutar bloque por bloque en SSMS.
   ===================================================================== */


/* PASO 1 · PAUSAR generación y envío */
UPDATE Procedimientos SET Activo = 0 WHERE Codigo IN ('GenerarResumen', 'EnvioResumen');
GO


/* PASO 2 · Ver qué se va a borrar (solo lectura) */
DECLARE @Vencimiento date = '2026-11-15';
DECLARE @PeriodoId int = (SELECT Id FROM Periodo WHERE CAST(FechaVencimiento AS date) = @Vencimiento);

SELECT @PeriodoId AS PeriodoId, (SELECT Descripcion FROM Periodo WHERE Id = @PeriodoId) AS Periodo;
SELECT 'ResumenTarjeta' AS Tabla, COUNT(*) AS Cantidad FROM ResumenTarjeta WHERE PeriodoId = @PeriodoId
UNION ALL SELECT 'ResumenGeneracion', COUNT(*) FROM ResumenGeneracion WHERE PeriodoId = @PeriodoId
UNION ALL SELECT 'EnvioResumenMail (todos)', COUNT(*) FROM EnvioResumenMail WHERE PeriodoId = @PeriodoId
UNION ALL SELECT '  de esos, ya enviados a clientes', COUNT(*) FROM EnvioResumenMail WHERE PeriodoId = @PeriodoId AND EsPrueba = 0 AND Estado IN ('Enviado', 'Enviando')
UNION ALL SELECT 'EnvioResumenMailIntento', COUNT(*) FROM EnvioResumenMailIntento i JOIN EnvioResumenMail e ON e.Id = i.EnvioResumenMailId WHERE e.PeriodoId = @PeriodoId
UNION ALL SELECT 'EjecucionProceso', COUNT(*) FROM EjecucionProceso WHERE PeriodoId = @PeriodoId;
GO


/* PASO 3 · BORRAR TODO (en una transacción, sin controlar si ya se envió) */
SET XACT_ABORT ON;
DECLARE @Vencimiento date = '2026-11-15';
DECLARE @PeriodoId int = (SELECT Id FROM Periodo WHERE CAST(FechaVencimiento AS date) = @Vencimiento);

IF @PeriodoId IS NULL
BEGIN
    RAISERROR('No existe el período con ese vencimiento. No se borró nada.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

    DELETE i FROM EnvioResumenMailIntento i
    JOIN EnvioResumenMail e ON e.Id = i.EnvioResumenMailId
    WHERE e.PeriodoId = @PeriodoId;

    DELETE FROM EnvioResumenMail WHERE PeriodoId = @PeriodoId;

    DELETE FROM ResumenGeneracion WHERE PeriodoId = @PeriodoId;

    DELETE d FROM DistribucionResumen d
    JOIN ResumenTarjeta r ON r.Id = d.ResumenTarjetaId
    WHERE r.PeriodoId = @PeriodoId;

    DELETE FROM ResumenTarjeta WHERE PeriodoId = @PeriodoId;

    DELETE FROM EjecucionProceso WHERE PeriodoId = @PeriodoId;

    UPDATE Procedimientos SET FechaUltimaEjecucionExitosa = NULL WHERE Codigo IN ('GenerarResumen', 'EnvioResumen');

COMMIT TRANSACTION;

SELECT 'Quedan en ResumenTarjeta' AS Control, COUNT(*) AS Cantidad FROM ResumenTarjeta WHERE PeriodoId = @PeriodoId
UNION ALL SELECT 'Quedan en ResumenGeneracion', COUNT(*) FROM ResumenGeneracion WHERE PeriodoId = @PeriodoId
UNION ALL SELECT 'Quedan en EnvioResumenMail', COUNT(*) FROM EnvioResumenMail WHERE PeriodoId = @PeriodoId
UNION ALL SELECT 'Quedan en EjecucionProceso', COUNT(*) FROM EjecucionProceso WHERE PeriodoId = @PeriodoId;
GO


/* PASO 4 · (después de publicar la versión nueva) REACTIVAR la generación HOY
   y reiniciar la app / reciclar el app pool para que arranque en el momento. */
UPDATE Procedimientos SET DiaEjecucion = DAY(GETDATE()), Activo = 1 WHERE Codigo = 'GenerarResumen';
SELECT Codigo, Activo, DiaEjecucion FROM Procedimientos WHERE Codigo = 'GenerarResumen';
GO


/* PASO 5 · Cuando la generación termine, volver el día de ejecución a 1 */
-- UPDATE Procedimientos SET DiaEjecucion = 1 WHERE Codigo = 'GenerarResumen';


/* PASO 6 · Activar el envío solo después de revisar lo generado
   (y con EnvioResumen:Modo = "Produccion" en el appsettings del servidor) */
-- UPDATE Procedimientos SET Activo = 1 WHERE Codigo = 'EnvioResumen';
