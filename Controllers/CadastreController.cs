using Microsoft.AspNetCore.Mvc;
using SmartKostanay.Services;
using System.Threading.Tasks;

namespace SmartKostanay.Controllers
{
    [ApiController]
    [Route("api/v1/izhs")]
    public class CadastreController : ControllerBase
    {
        private readonly CadastreService _cadastreService;

        public CadastreController(CadastreService cadastreService)
        {
            _cadastreService = cadastreService;
        }

        // GET: api/v1/izhs/land-plots
        [HttpGet("land-plots")]
        public async Task<IActionResult> GetLandPlots(
            [FromQuery] string? district = null,
            [FromQuery] string? status = null,
            [FromQuery] int? stage = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var (items, totalCount) = await _cadastreService.GetFilteredAsync(district, status, stage, page, pageSize);
            return Ok(new { totalCount, page, pageSize, items });
        }

        // GET: api/v1/izhs/land-plots/{id} 
        [HttpGet("land-plots/{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var plot = await _cadastreService.GetByIdAsync(id);
            if (plot == null)
                return NotFound(new { message = "Участок не найден" });

            return Ok(plot);
        }

        // GET: api/v1/izhs/by-cadaster/{cadNumber}
        [HttpGet("by-cadaster/{cadNumber}")]
        public async Task<IActionResult> GetByCadaster(string cadNumber)
        {
            var item = await _cadastreService.GetByCadNumberAsync(cadNumber);
            if (item == null)
                return NotFound(new { message = "Участок не найден по кадастровому номеру" });

            return Ok(item);
        }
    }
}