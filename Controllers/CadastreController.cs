using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver.GeoJsonObjectModel;
using SmartKostanay.Models;
using SmartKostanay.Models.DTOs;
using SmartKostanay.Services;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System;
using Microsoft.AspNetCore.Http;
using System.Linq;

using IoDirectory = System.IO.Directory;

namespace SmartKostanay.Controllers
{
    [ApiController]
    [Route("api/v1/izhs")]
    public class CadastreController : ControllerBase
    {
        private readonly CadastreService _cadastreService;
        private readonly EgknScraperService _egknService;

        public CadastreController(CadastreService cadastreService, EgknScraperService egknService)
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
            [FromQuery] string? searchTerm = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var (items, totalCount) = await _cadastreService.GetFilteredAsync(district, status, stage, searchTerm, page, pageSize);
            return Ok(new { totalCount, page, pageSize, items });
        }

        [HttpGet("land-plots/map")]
        public async Task<IActionResult> GetMapData([FromQuery] string? district, [FromQuery] string? status)
        {
            var result = await _cadastreService.GetMapDataAsync(district, status);
            return Ok(result);
        }

        [HttpGet("land-plots/{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var plot = await _cadastreService.GetByIdAsync(id);
            if (plot == null) return NotFound(new { message = "Участок не найден" });
            return Ok(plot);
        }

        [HttpGet("by-cadaster/{cadNumber}")]
        public async Task<IActionResult> GetByCadaster(string cadNumber)
        {
            var item = await _cadastreService.GetByCadNumberAsync(cadNumber);
            if (item == null) return NotFound(new { message = "Участок не найден по кадастровому номеру" });
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
                    Lastname = dto.Owner.Lastname,
                    Firstname = dto.Owner.Firstname,
                    Patronymic = dto.Owner.Patronymic,
                    IdentityNumber = dto.Owner.IdentityNumber,
                    PhoneNumber = dto.Owner.PhoneNumber
                },
                Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                    new GeoJson2DGeographicCoordinates(dto.Location.Coordinates[0], dto.Location.Coordinates[1]))
            };

            if (dto.Boundary != null && dto.Boundary.Any())
            {
                var points = dto.Boundary
                    .Select(c => new GeoJson2DGeographicCoordinates(c[0], c[1]))
                    .ToList();

                if (points.First().Values[0] != points.Last().Values[0] || points.First().Values[1] != points.Last().Values[1])
                    points.Add(points.First());

                var linearRing = new GeoJsonLinearRingCoordinates<GeoJson2DGeographicCoordinates>(points);
                var polygonCoordinates = new GeoJsonPolygonCoordinates<GeoJson2DGeographicCoordinates>(linearRing);
                plot.Boundary = new GeoJsonPolygon<GeoJson2DGeographicCoordinates>(polygonCoordinates);
            }

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

            var uploadsPath = Path.Combine(IoDirectory.GetCurrentDirectory(), "wwwroot", "uploads", "documents");
            if (!IoDirectory.Exists(uploadsPath)) IoDirectory.CreateDirectory(uploadsPath);

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
                UploadedBy = "Система",
                DateOfCreation = DateTime.UtcNow
            };

            await _cadastreService.SaveDocumentAsync(document);
            return Ok(document);
        }

        [HttpGet("documents/{docId}/download")]
        public async Task<IActionResult> DownloadDocument(string docId)
        {
            var doc = await _cadastreService.GetDocumentByIdAsync(docId);
            if (doc == null) return NotFound("Документ не найден");

            var filePath = Path.Combine(IoDirectory.GetCurrentDirectory(), "wwwroot", doc.FileUrl.TrimStart('/'));
            if (!System.IO.File.Exists(filePath)) return NotFound("Файл отсутствует");

            var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(filePath, out var contentType)) contentType = "application/octet-stream";

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, contentType, doc.FileName);
        }

        [HttpDelete("documents/{docId}")]
        public async Task<IActionResult> DeleteDocument(string docId)
        {
            var doc = await _cadastreService.GetDocumentByIdAsync(docId);
            if (doc == null) return NotFound("Документ не найден");

            var filePath = Path.Combine(IoDirectory.GetCurrentDirectory(), "wwwroot", doc.FileUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);

            await _cadastreService.DeleteDocumentAsync(docId);
            return Ok(new { message = "Документ удален" });
        }

        #endregion

        #region Экспорт и Синхронизация

        [HttpGet("land-plots/export")]
        public async Task<IActionResult> Export([FromQuery] string? district, [FromQuery] string? status)
        {
            var fileBytes = await _cadastreService.ExportToExcelAsync(district, status);
            string fileName = $"Otchet_IZHS_{DateTime.Now:dd-MM-yyyy}.xlsx";
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpPost("sync-egkn")]
        public async Task<IActionResult> SyncWithEgkn([FromQuery] string kadNumber)
        {
            if (string.IsNullOrEmpty(kadNumber)) return BadRequest(new { message = "Кадастровый номер не указан" });

            try
            {
                var coordsList = await _egknService.GetActualCoordinatesAsync(kadNumber);
                if (coordsList == null || !coordsList.Any()) return NotFound("Геометрия не найдена");

                var center = coordsList.First();
                var polygonPoints = coordsList.Select(c => new GeoJson2DGeographicCoordinates(c.lon, c.lat)).ToList();

                if (polygonPoints.First().Values[0] != polygonPoints.Last().Values[0] || polygonPoints.First().Values[1] != polygonPoints.Last().Values[1])
                    polygonPoints.Add(polygonPoints.First());

                var linearRing = new GeoJsonLinearRingCoordinates<GeoJson2DGeographicCoordinates>(polygonPoints);
                var polygonCoordinates = new GeoJsonPolygonCoordinates<GeoJson2DGeographicCoordinates>(linearRing);
                var polygon = new GeoJsonPolygon<GeoJson2DGeographicCoordinates>(polygonCoordinates);

                var newPlot = new IzhsLandPlot
                {
                    CadastralNumber = kadNumber,
                    Address = "Синхронизировано из ЕГКН",
                    OverallStatus = "ACTIVE",
                    DateOfCreation = DateTime.UtcNow,
                    ModifiedOn = DateTime.UtcNow,
                    Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                        new GeoJson2DGeographicCoordinates(center.lon, center.lat)),
                    Boundary = polygon
                };

                await _cadastreService.CreateAsync(newPlot);
                return Ok(new { message = "Синхронизация успешна", id = newPlot.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка", details = ex.Message });
            }
        }

        #endregion

        #region ФОТО

        [HttpPost("land-plots/{id}/photos")]
        public async Task<IActionResult> UploadPhoto(
            string id,
            [FromForm] IFormFile file,
            [FromForm] int stageNumber,
            [FromForm] string? comment)
        {
            if (file == null || file.Length == 0) return BadRequest("Файл не выбран");

            var extension = Path.GetExtension(file.FileName).ToLower();
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".heic")
                return BadRequest("Допустимы только форматы JPEG и HEIC");

            var plot = await _cadastreService.GetByIdAsync(id);
            if (plot == null) return NotFound("Участок не найден");

            var fileName = $"{Guid.NewGuid()}{extension}";
            var folderPath = Path.Combine(IoDirectory.GetCurrentDirectory(), "wwwroot/uploads/photos");
            if (!IoDirectory.Exists(folderPath)) IoDirectory.CreateDirectory(folderPath);
            var filePath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var photo = new IzhsPhoto
            {
                LandPlotId = id,
                StageNumber = stageNumber,
                FileUrl = $"/uploads/photos/{fileName}",
                OriginalFileName = file.FileName,
                FileSize = file.Length,
                Comment = comment,
                DateOfCreation = DateTime.UtcNow
            };

            try
            {
                var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(filePath);
                var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                var gps = directories.OfType<GpsDirectory>().FirstOrDefault();

                if (subIfd != null)
                {
                    var dateTag = subIfd.GetDescription(ExifDirectoryBase.TagDateTimeOriginal);
                    photo.Metadata.CapturedAt = dateTag != null
                        ? DateTime.ParseExact(dateTag, "yyyy:MM:dd HH:mm:ss", null)
                        : (DateTime?)null;
                    photo.Metadata.HasExif = true;
                }

                if (gps != null)
                {
                    var loc = gps.GetGeoLocation();
                    if (loc is GeoLocation geoLoc)
                    {
                        photo.Metadata.GeoLocation = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                            new GeoJson2DGeographicCoordinates(geoLoc.Longitude, geoLoc.Latitude));
                    }
                }
            }
            catch
            {
                photo.Metadata.HasExif = false;
            }

            photo.Validation.IsGeoPresent = photo.Metadata.GeoLocation != null;
            photo.Validation.IsDatePresent = photo.Metadata.CapturedAt.HasValue;

            if (photo.Validation.IsGeoPresent)
            {
                var currentLon = photo.Metadata.GeoLocation.Coordinates.Values[0];
                var currentLat = photo.Metadata.GeoLocation.Coordinates.Values[1];
                photo.Validation.IsWithinBoundary = await _cadastreService.IsPointInPlotBoundary(id, currentLon, currentLat);
            }

            if (photo.Validation.IsDatePresent)
            {
                photo.Validation.TimeDriftSeconds = (int)Math.Abs((DateTime.UtcNow - photo.Metadata.CapturedAt.Value).TotalSeconds);
                photo.Validation.IsTimeDriftAcceptable = photo.Validation.TimeDriftSeconds <= (72 * 3600);
            }

            photo.Validation.IsValid = photo.Validation.IsGeoPresent &&
                                        photo.Validation.IsWithinBoundary &&
                                        photo.Validation.IsDatePresent &&
                                        photo.Validation.IsTimeDriftAcceptable;

            await _cadastreService.SavePhotoAsync(photo);
            return Ok(photo);
        }

        [HttpGet("land-plots/{id}/photos")]
        public async Task<IActionResult> GetPhotos(string id)
        {
            return Ok(await _cadastreService.GetPhotosByPlotIdAsync(id));
        }

        [HttpGet("photos/{photoId}")]
        public async Task<IActionResult> GetPhotoDetails(string photoId)
        {
            var photo = await _cadastreService.GetPhotoByIdAsync(photoId);
            return photo == null ? NotFound() : Ok(photo);
        }

        [HttpDelete("photos/{photoId}")]
        public async Task<IActionResult> DeletePhoto(string photoId)
        {
            var photo = await _cadastreService.GetPhotoByIdAsync(photoId);
            if (photo != null)
            {
                var fullPath = Path.Combine(IoDirectory.GetCurrentDirectory(), "wwwroot", photo.FileUrl.TrimStart('/'));
                if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                await _cadastreService.DeletePhotoAsync(photoId);
            }
            return Ok(new { message = "Удалено" });
        }

        #endregion

        #region Журнал действий (Audit Log)

        [HttpGet("land-plots/{id}/audit-log")]
        public async Task<IActionResult> GetAuditLog(string id)
        {
            var plot = await _cadastreService.GetByIdAsync(id);
            if (plot == null) return NotFound(new { message = "Участок не найден" });

            var logs = await _cadastreService.GetAuditLogByPlotIdAsync(id);
            return Ok(logs);
        }

        #endregion
    }

    public class ExclusionRequest
    {
        public string Reason { get; set; } = string.Empty;
    }
}