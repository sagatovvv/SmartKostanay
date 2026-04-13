using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver.GeoJsonObjectModel;
using SmartKostanay.Models;
using SmartKostanay.Models.DTOs;
using SmartKostanay.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace SmartKostanay.Controllers
{
    [ApiController]
    [Route("api/v1/izhs")]
    public class CadastreController : ControllerBase
    {
        private readonly CadastreService _cadastreService;
        private readonly EgknIntegrationService _egknService;

        public CadastreController(CadastreService cadastreService, EgknIntegrationService egknService)
        {
            _cadastreService = cadastreService;
            _egknService = egknService;
        }

        #region Участки (Land Plots)

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

        [HttpGet("/api/v1/izhs/land-plots/map")]
        public async Task<IActionResult> GetMapData([FromQuery] string? district, [FromQuery] string? status)
        {
            var result = await _cadastreService.GetMapDataAsync(district, status);
            return Ok(result);
        }

        [HttpGet("land-plots/{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var plot = await _cadastreService.GetByIdAsync(id);
            if (plot == null)
                return NotFound(new { message = "Участок не найден" });

            return Ok(plot);
        }

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
            if (dto == null) return BadRequest(new { message = "Данные участка не заполнены" });

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
                Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                    new GeoJson2DGeographicCoordinates(dto.Location.Coordinates[0], dto.Location.Coordinates[1]))
            };

            await _cadastreService.CreateAsync(plot);
            return CreatedAtAction(nameof(GetById), new { id = plot.Id }, plot);
        }

        [HttpPatch("land-plots/{id}/exclude")]
        public async Task<IActionResult> Exclude(string id, [FromBody] ExclusionRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Reason))
                return BadRequest(new { message = "Причина исключения обязательна" });

            var success = await _cadastreService.ExcludeAsync(id, request.Reason);
            if (!success) return NotFound(new { message = "Участок не найден" });

            return Ok(new { message = "Участок успешно исключен из контроля" });
        }

        #endregion

        #region Документы (Documents)

        // POST: api/v1/izhs/land-plots/{id}/documents
        [HttpPost("land-plots/{id}/documents")]
        public async Task<IActionResult> UploadDocument(
            string id,
            [FromForm] IFormFile file,
            [FromForm] int stageNumber,
            [FromForm] string type,
            [FromForm] string? comment)
        {
            if (file == null || file.Length == 0) return BadRequest("Файл не выбран");

            var plot = await _cadastreService.GetByIdAsync(id);
            if (plot == null) return NotFound("Участок не найден");

            // Путь: wwwroot/uploads/documents
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "documents");
            if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);

            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsPath, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var document = new LandDocument
            {
                LandPlotId = id,
                StageNumber = stageNumber,
                Type = type,
                FileName = file.FileName,
                FileUrl = $"/uploads/documents/{uniqueFileName}",
                FileSize = file.Length,
                Comment = comment,
                UploadSource = "MOBILE",
                UploadedBy = "65f1a5f9e4b0c1234567890a", // В реале берем из User.Identity
                DateOfCreation = DateTime.UtcNow
            };

            await _cadastreService.SaveDocumentAsync(document);
            return Ok(document);
        }

        // GET: api/v1/izhs/documents/{docId}/download
        [HttpGet("documents/{docId}/download")]
        public async Task<IActionResult> DownloadDocument(string docId)
        {
            // 1. Ищем инфу о файле в базе
            var doc = await _cadastreService.GetDocumentByIdAsync(docId);
            if (doc == null) return NotFound("Документ не найден в базе данных");

            // 2. Формируем полный путь к файлу на сервере
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", doc.FileUrl.TrimStart('/'));

            if (!System.IO.File.Exists(filePath))
                return NotFound("Файл физически отсутствует на сервере");

            // 3. Определяем MIME-тип (pdf, png и т.д.)
            var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(filePath, out var contentType))
            {
                contentType = "application/octet-stream"; // Если тип неизвестен, отдаем как поток байтов
            }

            // 4. Отдаем файл пользователю
            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

            // Этот метод заставит браузер открыть окно сохранения с оригинальным именем файла
            return File(fileBytes, contentType, doc.FileName);
        }


        // GET: api/v1/izhs/land-plots/{id}/documents
        [HttpGet("land-plots/{id}/documents")]
        public async Task<IActionResult> GetDocuments(string id)
        {
            var docs = await _cadastreService.GetDocumentsByPlotIdAsync(id);
            return Ok(docs);
        }

        // DELETE: api/v1/izhs/documents/{docId}
        [HttpDelete("documents/{docId}")]
        public async Task<IActionResult> DeleteDocument(string docId)
        {
            var doc = await _cadastreService.GetDocumentByIdAsync(docId);
            if (doc == null) return NotFound("Документ не найден");

            // Удаляем файл физически
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", doc.FileUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);

            await _cadastreService.DeleteDocumentAsync(docId);
            return Ok(new { message = "Документ удален" });
        }

        #endregion

        #region Экспорт и Синхронизация

        [HttpGet("/api/v1/izhs/land-plots/export")]
        public async Task<IActionResult> Export([FromQuery] string? district, [FromQuery] string? status)
        {
            var fileBytes = await _cadastreService.ExportToExcelAsync(district, status);
            string fileName = $"Otchet_IZHS_{DateTime.Now:dd-MM-yyyy}.xlsx";
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpPost("sync-egkn")]
        public async Task<IActionResult> SyncWithEgkn([FromQuery] string kadNumber)
        {
            if (string.IsNullOrEmpty(kadNumber))
            {
                return BadRequest(new { message = "Кадастровый номер не указан" });
            }

            try
            {
                // 1. Запрашиваем данные из ЕГКН
                var egknData = await _egknService.GetLandByCadastre(kadNumber);

                if (egknData == null)
                {
                    return NotFound(new { message = "Участок с таким номером не найден в базе ЕГКН" });
                }

                // 2. Парсим геометрию (используем .Geometry, как в твоем исходнике)
                var coords = _egknService.GetCenterCoordinates(egknData.Geometry);

                // 3. Создаем модель для нашей MongoDB
                var newPlot = new IzhsLandPlot
                {
                    CadastralNumber = egknData.Properties.CadastralNumber,
                    Address = egknData.Properties.Address,
                    Area = egknData.Properties.Area,
                    District = egknData.Properties.DistrictId.ToString(),
                    OverallStatus = "IN_PROGRESS",
                    DateOfCreation = DateTime.UtcNow,

                    Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                        new GeoJson2DGeographicCoordinates(coords.lon, coords.lat)
                    )
                };

                // 4. Сохраняем в базу
                await _cadastreService.CreateAsync(newPlot);

                return Ok(new
                {
                    message = "Участок успешно синхронизирован и добавлен",
                    plot = newPlot
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка при синхронизации", details = ex.Message });
            }
        }

        #endregion
    }

    public class ExclusionRequest
    {
        public string Reason { get; set; } = string.Empty;
    }
}