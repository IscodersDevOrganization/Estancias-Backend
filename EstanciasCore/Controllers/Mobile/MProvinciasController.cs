using DAL.Data;
using DAL.Mobile;
using DAL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using EstanciasCore.API.Controllers;
using System.Collections.Generic;
using System.Linq;

namespace EstanciasCore.Controllers.Mobile
{
    [Route("api/[controller]")]
    public class MProvinciasController : BaseApiController
    {
        public MProvinciasController(EstanciasContext context):base(context)
        {

        }
        [HttpPost]
        [Route("TraeProvincias")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public MTraeProvinciasDTO TraeProvincias([FromBody] MTraeProvinciasDTO uat)
        {
            //var Uat = _context.UAT.FirstOrDefault(x => x.Token == uat.UAT);
            //if (Uat == null)
            //{
            //    uat.Status = 500;
            //    uat.Mensaje = "UAT Invalida";
            //    return uat;
            //}
            uat.Status = 200;
            uat.Mensaje = "Listado de Provincias";
            IEnumerable<Provincia> Provincias;
            if (uat.ProvinciaId != null && uat.ProvinciaId != 0)
            {
                Provincias = _context.Provincia.Where(x => x.Id == uat.ProvinciaId);
            }
            else
            {
                Provincias = _context.Provincia;
            }
            List<ProvinciaDTO> lista = new List<ProvinciaDTO>();
            foreach (var Provincia in Provincias)
            {
                var renglon = new ProvinciaDTO();
                renglon.Id = Provincia.Id;
                renglon.Latitud = Provincia.Latitud;
                renglon.Longitud = Provincia.Longitud;
                renglon.Descripcion = Provincia.Descripcion;
                renglon.DescripcionCompleta = Provincia.DescripcionCompleta;
                lista.Add(renglon);
            }
            uat.Provincias = lista;
            return uat;
        }
    }
}