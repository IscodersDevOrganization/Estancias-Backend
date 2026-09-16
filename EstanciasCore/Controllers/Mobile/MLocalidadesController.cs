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
    public class MLocalidadesController : BaseApiController
    {
        public MLocalidadesController(EstanciasContext context) : base(context)
        {

        }
        [HttpPost]
        [Route("TraeLocalidades")]
        [EnableCors("CorsPolicy")]
        [AllowAnonymous]
        public MTraeLocalidadDTO TraeLocalidades([FromBody] MTraeLocalidadDTO uat)
        {
            //var Uat = _context.UAT.FirstOrDefault(x => x.Token == uat.UAT);
            //if (Uat == null)
            //{
            //    uat.Status = 500;
            //    uat.Mensaje = "UAT Invalida";
            //    return uat;
            //}
            uat.Status = 200;
            uat.Mensaje = "Listado de Localidades";
            IEnumerable<Localidad> Localidades;
            if (uat.LocalidadId != null && uat.LocalidadId != 0)
            {
                Localidades = _context.Localidad.Where(x => x.Id == uat.LocalidadId);
            }
            else if (uat.ProvinciaId != null && uat.ProvinciaId != 0)
            {
                Localidades = _context.Localidad.Where(x => x.IdProvincia == uat.ProvinciaId);
            }
            else
            {
                Localidades = _context.Localidad;
            }

            List<LocalidadDTO> lista = new List<LocalidadDTO>();
            foreach (var localidad in Localidades)
            {
                var renglon = new LocalidadDTO();
                renglon.Id = localidad.Id;
                renglon.Latitud = localidad.Latitud;
                renglon.Longitud = localidad.Longitud;
                renglon.NombreLocalidad = localidad.Descripcion;
                renglon.IdDepartamento = localidad.IdDepartamento;
                renglon.IdProvincia = localidad.IdProvincia;
                renglon.NombreProvincia = localidad.ProvinciaNombre;               
                lista.Add(renglon);
            }
            uat.Localidades = lista;
            return uat;
        }
    }
}
