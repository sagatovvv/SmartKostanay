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

        [HttpGet("land-plots")]
        public async Task<IActionResult> GetLandPlots(
            [FromQuery] string district,
            [FromQuery] string status,
            [FromQuery] int? stage,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var (items, totalCount) = await _cadastreService.GetFilteredAsync(district, status, stage, page, pageSize);

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                items
            });
        }

        [HttpGet("{cadNumber}")]
        public async Task<IActionResult> Get(string cadNumber)
        {
            // Используем новый асинхронный метод
            var item = await _cadastreService.GetByCadNumberAsync(cadNumber);

            if (item == null)
                return NotFound(new { message = "Участок не найден" });


            double lon = item.Location.Coordinates.Longitude;
            double lat = item.Location.Coordinates.Latitude;

            return Ok(new
            {
                cadastralNumber = item.CadastralNumber,
                address = item.Address,
                district = item.District,
                area = item.Area,
                status = item.OverallStatus,
                currentStage = item.CurrentStage,
                lat,
                lon
            });
        }
    }
}