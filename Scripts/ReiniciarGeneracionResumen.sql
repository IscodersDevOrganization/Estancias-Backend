/* =====================================================================
   REINICIAR LA GENERACIÓN DE RESÚMENES DE UN PERÍODO
   ---------------------------------------------------------------------
   Ejecutar paso por paso (seleccionar cada bloque y F5 en SSMS).
   Cada bloque declara sus variables, así se puede correr por separado.
   Período por defecto: el que vence el 15/11/2026 (generado el 01/11).
   ===================================================================== */


/* ---------------------------------------------------------------------
   PASO 0 · DIAGNÓSTICO (solo lectura, no modifica nada)
   --------------------------------------------------------------------- */
DECLARE @Vencimiento date = '2026-11-15';
DECLARE @PeriodoId int = (SELECT Id FROM Periodo WHERE CAST(FechaVencimiento AS date) = @Vencimiento);

SELECT @PeriodoId AS PeriodoId, (SELECT Descripcion FROM Periodo WHERE Id = @PeriodoId) AS Periodo;

SELECT 'Resúmenes guardados (ResumenTarjeta)'             AS Concepto, COUNT(*) AS Cantidad FROM ResumenTarjeta    WHERE PeriodoId = @PeriodoId
UNION ALL SELECT 'Generación OK',                          COUNT(*) FROM ResumenGeneracion WHERE PeriodoId = @PeriodoId AND Estado = 'Generado'
UNION ALL SELECT 'Generación con error',                   COUNT(*) FROM ResumenGeneracion WHERE PeriodoId = @PeriodoId AND Estado = 'Fallido'
UNION ALL SELECT 'Cola: pendientes',                       COUNT(*) FROM EnvioResumenMail  WHERE PeriodoId = @PeriodoId AND EsPrueba = 0 AND Estado IN ('Pendiente', 'Fallido')
UNION ALL SELECT '*** Cola: YA ENVIADOS A CLIENTES ***',   COUNT(*) FROM EnvioResumenMail  WHERE PeriodoId = @PeriodoId AND EsPrueba = 0 AND Estado IN ('Enviado', 'Enviando')
UNION ALL SELECT 'Cola: envíos de prueba',                 COUNT(*) FROM EnvioResumenMail  WHERE PeriodoId = @PeriodoId AND EsPrueba = 1;

-- Errores agrupados (para confirmar que es el problema de la API de LOAN)
SELECT CodigoError, COUNT(*) AS Cantidad, MIN(Mensaje) AS EjemploMensaje
FROM ResumenGeneracion
WHERE PeriodoId = @PeriodoId AND Estado = 'Fallido'
GROUP BY CodigoError
ORDER BY Cantidad DESC;

-- Algunos clientes que fallaron: copiá 2 o 3 DNI para probarlos con "Simular resumen" antes de regenerar
SELECT TOP 10 p.NroDocumento, p.NroTarjeta, u.UserName, g.CodigoError, g.Mensaje
FROM ResumenGeneracion g
JOIN AspNetUsers u ON u.Id = g.UsuarioId
LEFT JOIN Personas p ON p.Id = u.PersonasId
WHERE g.PeriodoId = @PeriodoId AND g.Estado = 'Fallido';

-- Corridas del día
SELECT * FROM EjecucionProceso WHERE PeriodoId = @PeriodoId ORDER BY Id;

SELECT Codigo, Activo, DiaEjecucion, FechaUltimaEjecucionExitosa FROM Procedimientos WHERE Codigo IN ('GenerarResumen', 'EnvioResumen');
GO


/* ---------------------------------------------------------------------
   PASO 1 · PAUSAR generación y envío (para que nada corra mientras limpiás)
   --------------------------------------------------------------------- */
UPDATE Procedimientos SET Activo = 0 WHERE Codigo IN ('GenerarResumen', 'EnvioResumen');
SELECT Codigo, Activo FROM Procedimientos WHERE Codigo IN ('GenerarResumen', 'EnvioResumen');
GO


/* ---------------------------------------------------------------------
   PASO 2 · BORRAR todo lo generado del período (en una transacción)
   Se cancela solo si algún resumen ya se le envió a un cliente.
   --------------------------------------------------------------------- */
SET XACT_ABORT ON;
DECLARE @Vencimiento date = '2026-11-15';
DECLARE @PeriodoId int = (SELECT Id FROM Periodo WHERE CAST(FechaVencimiento AS date) = @Vencimiento);

IF @PeriodoId IS NULL
BEGIN
    RAISERROR('No existe el período con ese vencimiento. No se borró nada.', 16, 1);
    RETURN;
END

IF EXISTS (SELECT 1 FROM EnvioResumenMail WHERE PeriodoId = @PeriodoId AND EsPrueba = 0 AND Estado IN ('Enviado', 'Enviando'))
BEGIN
    RAISERROR('Hay resúmenes de este período YA ENVIADOS a clientes. No se borró nada: revisá el PASO 0 antes de seguir.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

    -- 1) Historial de intentos y cola de envío (incluye los envíos de prueba de este período)
    DELETE i FROM EnvioResumenMailIntento i
    JOIN EnvioResumenMail e ON e.Id = i.EnvioResumenMailId
    WHERE e.PeriodoId = @PeriodoId;

    DELETE FROM EnvioResumenMail WHERE PeriodoId = @PeriodoId;

    -- 2) Estado de generación por usuario
    DELETE FROM ResumenGeneracion WHERE PeriodoId = @PeriodoId;

    -- 3) Tabla vieja de distribución (por si quedó algo apuntando a estos resúmenes)
    DELETE d FROM DistribucionResumen d
    JOIN ResumenTarjeta r ON r.Id = d.ResumenTarjetaId
    WHERE r.PeriodoId = @PeriodoId;

    -- 4) Los resúmenes (PDF)
    DELETE FROM ResumenTarjeta WHERE PeriodoId = @PeriodoId;

    -- 5) Corridas del período: así el límite de 3 intentos por día vuelve a cero
    DELETE FROM EjecucionProceso WHERE PeriodoId = @PeriodoId;

    -- 6) Que la generación no figure como "ya ejecutada hoy"
    UPDATE Procedimientos
    SET FechaUltimaEjecucionExitosa = NULL
    WHERE Codigo = 'GenerarResumen'
      AND CAST(FechaUltimaEjecucionExitosa AS date) = CAST(GETDATE() AS date);

COMMIT TRANSACTION;

SELECT 'Quedan en ResumenTarjeta'    AS Control, COUNT(*) AS Cantidad FROM ResumenTarjeta    WHERE PeriodoId = @PeriodoId
UNION ALL SELECT 'Quedan en ResumenGeneracion', COUNT(*) FROM ResumenGeneracion WHERE PeriodoId = @PeriodoId
UNION ALL SELECT 'Quedan en EnvioResumenMail',  COUNT(*) FROM EnvioResumenMail  WHERE PeriodoId = @PeriodoId
UNION ALL SELECT 'Quedan en EjecucionProceso',  COUNT(*) FROM EjecucionProceso  WHERE PeriodoId = @PeriodoId;
GO


/* ---------------------------------------------------------------------
   PASO 3 · (en la app) Probar 2 o 3 clientes con "Simular resumen"
   para confirmar que la API de LOAN ya responde bien.
   --------------------------------------------------------------------- */


/* ---------------------------------------------------------------------
   PASO 4 · REACTIVAR la generación (tiene que ser HOY, día 1)
   El worker revisa una vez por hora; para que arranque en el momento,
   reiniciá la aplicación (o reciclá el app pool en IIS).
   --------------------------------------------------------------------- */
UPDATE Procedimientos SET Activo = 1 WHERE Codigo = 'GenerarResumen';
SELECT Codigo, Activo, DiaEjecucion FROM Procedimientos WHERE Codigo = 'GenerarResumen';
GO

/* Si ya pasó el día 1 (por ejemplo, lo hacés el 2), la generación no corre sola.
   Cambiá temporalmente el día al de hoy, esperá a que termine y volvelo a 1:

   UPDATE Procedimientos SET DiaEjecucion = DAY(GETDATE()) WHERE Codigo = 'GenerarResumen';
   -- ... cuando termine:
   UPDATE Procedimientos SET DiaEjecucion = 1 WHERE Codigo = 'GenerarResumen';
*/


/* ---------------------------------------------------------------------
   PASO 5 · REACTIVAR el envío, solo cuando hayas revisado lo generado
   (y si EnvioResumen:Modo está en "Produccion" en el appsettings del servidor)
   --------------------------------------------------------------------- */
-- UPDATE Procedimientos SET Activo = 1 WHERE Codigo = 'EnvioResumen';
