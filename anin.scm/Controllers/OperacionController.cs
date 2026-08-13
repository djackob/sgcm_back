using anin.dataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace anin_scm.Controllers
{
    [Authorize]
    public class OperacionController : ControllerBase
    {
        [HttpGet]
        [AllowAnonymous]
        public IActionResult listarGastoSiafCombo()
        {
            try
            {
                DaProceso _Daproceso = new DaProceso();
                var strResultado = JsonDocument.Parse(_Daproceso.ejecutarProceso(
                    "cnx_sgp", "sgp.fn_listar_tm_sgp_gasto_siaf_combo"));
                if (strResultado != null)
                {
                    return Ok(strResultado);
                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        [HttpGet]
        public IActionResult listarGastoSiaf(string ipInput)
        {
            try
            {
                DaProceso _Daproceso = new DaProceso();
                var strResultado = JsonDocument.Parse(_Daproceso.ejecutarProceso(
                    "cnx_sgp", "sgp.fn_listar_tm_sgp_gasto_siaf", ipInput));
                if (strResultado != null)
                {
                    return Ok(strResultado);
                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        [HttpPost]
        public IActionResult insertarMeta(string ipInput)
        {
            try
            {
                DaProceso _Daproceso = new DaProceso();
                var strResultado = JsonDocument.Parse(_Daproceso.ejecutarProceso(
                    "cnx_sgp", "sgp.fn_insertar_td_sgp_meta", ipInput));
                if (strResultado != null)
                {
                    return Ok(strResultado);
                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        #region "Mantenedor de Meta Sector Paquete Proyecto"
        [HttpGet]
        public IActionResult listarMetaSectorPaqueteProyecto(string ipInput)
        {
            try
            {
                DaProceso _Daproceso = new DaProceso();
                var strResultado = JsonDocument.Parse(_Daproceso.ejecutarProceso(
                    "cnx_sgp", "spg.fn_listar_tm_spg_meta_sector_paquete_proyecto", ipInput));
                if (strResultado != null)
                {
                    return Ok(strResultado);
                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        [HttpPost]
        public IActionResult procesarMetaSectorPaqueteProyecto(string ipInput)
        {
            try
            {
                DaProceso _Daproceso = new DaProceso();
                var strResultado = JsonDocument.Parse(_Daproceso.ejecutarProceso(
                    "cnx_sgp", "spg.fn_procesar_tm_spg_meta_sector_paquete_proyecto", ipInput));
                if (strResultado != null)
                {
                    return Ok(strResultado);
                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }
        #endregion
    }
}
