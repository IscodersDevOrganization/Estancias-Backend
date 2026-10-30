using System;
using System.Threading;
using System.Threading.Tasks;

namespace EstanciasCore.Services
{
    /// <summary>
    /// Aviso en memoria (singleton) para que el EnvioDeResumenWorker revise la cola
    /// apenas se genera un resumen, sin esperar al próximo intervalo.
    /// Si la app se reinicia no se pierde nada: los pendientes siguen en la base.
    /// </summary>
    public class ColaEnvioResumenSignal
    {
        private readonly SemaphoreSlim _senal = new SemaphoreSlim(0, 1);

        public void Notificar()
        {
            try
            {
                if (_senal.CurrentCount == 0) _senal.Release();
            }
            catch (SemaphoreFullException)
            {
                // Ya había un aviso pendiente
            }
        }

        public async Task EsperarAsync(TimeSpan maximo, CancellationToken cancellationToken)
        {
            try
            {
                await _senal.WaitAsync(maximo, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Se está deteniendo la aplicación
            }
        }
    }
}
