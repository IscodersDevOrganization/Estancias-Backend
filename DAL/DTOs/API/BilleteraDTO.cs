using DAL.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DAL.DTOs.API
{
    public class SaldoDTO : RespuestaAPI
    {
        public decimal Saldo { get; set; }
    }

    public class CVUBilleteraDTO : RespuestaAPI
    {
        public string CVU { get; set; }
        public string Alias { get; set; }
    }


    public class ListaMovimientoDTO : RespuestaAPI
    {
        public List<MovimientoBilleteraDTO> Movimientos { get; set; }
    }

    public class MovimientoBilleteraDTO
    {
        public decimal Monto { get; set; }
        public string TipoMovimiento { get; set; }
        public DateTime Fecha { get; set; }
    }

    public class ListaTarjetasDTO : RespuestaAPI
    {
        public List<TarjetasBilleteraDTO> Tarjetas { get; set; }
    }

    public class TarjetasBilleteraDTO
    {
        public string Titular { get; set; }
        public string Numero { get; set; }
        public string Vencimiento { get; set; }
    }

    public class ListaCuentasDTO : RespuestaAPI
    {
        public List<CuentaBilleteraDTO> Cuentas { get; set; }
    }

    public class CuentaBilleteraDTO
    {
        public string CBU { get; set; }
        public string Descripcion { get; set; }
    }

    public class ListaBilleterasDTO : RespuestaAPI
    {
        public List<BilleteraAsociadaDTO> Billeteras { get; set; }
    }

    public class BilleteraAsociadaDTO
    {
        public string Titular { get; set; }
        public string CVU { get; set; }
    }


    public class ListaMediosPagoDTO : RespuestaAPI
    {
        public List<MedioPagoDTO> MediosPago { get; set; }
    }

    public class MedioPagoDTO
    {
        public int Id { get; set; }
        public TipoMedioPago TipoMedioPago { get; set; }
        public string Descripcion { get; set; }
        public string DetalleAdicional { get; set; }
    }

    public enum TipoMedioPago
    {
        TipoBilletera = 1,
        TipoTarjeta = 2,
        TipoCuenta = 3
    }

    public class EnvioBilleteraDTO : RespuestaAPI
    {
        public string CVU { get; set; }
        public string Monto { get; set; }
    }

    public class ClienteBilleteraDTO : RespuestaAPI
    {
        public string CVU { get; set; }
        public string Nombre { get; set; }
        public string Foto { get; set; }
    }


    public class AltaTarjetaDTO : RespuestaAPI
    {
        public string Numero { get; set; }
        public string Titular { get; set; }
        public string Vencimiento { get; set; }
    }

    public class AltaCuentaDTO : RespuestaAPI
    {
        public string Numero { get; set; }
        public string Titular { get; set; }
        public string CBU { get; set; }
        public string Alias { get; set; }
        public bool Terceros { get; set; }
    }

    public class DetallePagoBarrasDTO : RespuestaAPI
    {
        public string CodigoBarras { get; set; }
        public string NombreServicio { get; set; }
        public decimal Monto { get; set; }
    }

    public class ConfirmaPagoBarrasDTO : RespuestaAPI
    {
        public string CodigoBarras { get; set; }
        public int MetodoPagoId { get; set; }
        public TipoMedioPago TipoMedioPago { get; set; }
    }

    public class IngresoDineroDTO : RespuestaAPI
    {
        public string Monto { get; set; }
        public int MetodoPagoId { get; set; }
        public TipoMedioPago TipoMedioPago { get; set; }
    }


    public class RetiroDineroDTO : RespuestaAPI
    {
        public string Monto { get; set; }
        public int MetodoPagoId { get; set; }
        public TipoMedioPago TipoMedioPago { get; set; }
    }

    public class SolicitarTarjetaDTO : RespuestaAPI
    {
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string DNI { get; set; }
        public string Telefono { get; set; }
        public string Email { get; set; }
        public byte[] FrenteDNI { get; set; }
        public byte[] DorsoDNI { get; set; }
        public byte[] Selfie { get; set; }
        public DateTime FechaNacimiento { get; set; }
        public string Calle { get; set; }
        public string Altura { get; set; }
        public string PisoDepto { get; set; }
        public int LocalidadId { get; set; }
        public int ProvinciaId { get; set; }
        public string CodigoPostal { get; set; }

    }


}
