using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver.GeoJsonObjectModel;
using SmartKostanay.Models;
using SmartKostanay.Models.DTOs;
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


        [HttpPost("land-plots")]
        public async Task<IActionResult> Create([FromBody] LandPlotUpsertDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new { message = "Данные участка не заполнены" });
            }

        // 1. Превращаем DTO в модель базы данных (Маппинг)
            var plot = new IzhsLandPlot
                {
                CadastralNumber = dto.CadastralNumber,
                Address = dto.Address,
                District = dto.District,
                Area = dto.Area,
                IssueDate = dto.IssueDate,
                Owner = new OwnerDetails
                {
                    FullName = dto.Owner.FullName,
                    IdentityNumber = dto.Owner.IdentityNumber
                },
                // 2. Решаем проблему с GeoJson: конвертируем координаты из DTO в формат MongoDB
                Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                    new GeoJson2DGeographicCoordinates(dto.Location.Coordinates[0], dto.Location.Coordinates[1]))
            };

        // 3. Вызываем сервис (в нем создадутся этапы и дедлайны)
        await _cadastreService.CreateAsync(plot);

        return CreatedAtAction(nameof(GetById), new { id = plot.Id }, plot);
    }

        [HttpPut("land-plots/{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] LandPlotUpsertDto dto)
        {
            if (dto == null) return BadRequest();

            // 1. Сначала находим существующий участок в базе
            var existingPlot = await _cadastreService.GetByIdAsync(id);
            if (existingPlot == null)
            {
                return NotFound(new { message = "Участок для обновления не найден" });
            }

            // 2. Обновляем поля из DTO
            existingPlot.CadastralNumber = dto.CadastralNumber;
            existingPlot.Address = dto.Address;
            existingPlot.District = dto.District;
            existingPlot.Area = dto.Area;
            existingPlot.IssueDate = dto.IssueDate;

            existingPlot.Owner = new OwnerDetails
            {
                FullName = dto.Owner.FullName,
                IdentityNumber = dto.Owner.IdentityNumber
            };

            // Обновляем координаты (упаковываем массив в GeoJsonPoint)
            existingPlot.Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                new GeoJson2DGeographicCoordinates(dto.Location.Coordinates[0], dto.Location.Coordinates[1]));

            existingPlot.ModifiedOn = DateTime.UtcNow;

            // 3. Сохраняем обновленный объект через сервис
            var success = await _cadastreService.UpdateAsync(id, existingPlot);

            if (!success) return BadRequest(new { message = "Ошибка при обновлении в базе данных" });

            return NoContent(); // Статус 204
        }

        // 3. Исключение участка из контроля
        [HttpPatch("land-plots/{id}/exclude")]
        public async Task<IActionResult> Exclude(string id, [FromBody] ExclusionRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Reason))
            {
                return BadRequest(new { message = "Причина исключения обязательна" });
            }

            var success = await _cadastreService.ExcludeAsync(id, request.Reason);

            if (!success)
            {
                return NotFound(new { message = "Участок не найден или возникла ошибка" });
            }

            return Ok(new { message = "Участок успешно исключен из контроля" });
        }

    }
    public class ExclusionRequest
    {
        public string Reason { get; set; }
    }
}