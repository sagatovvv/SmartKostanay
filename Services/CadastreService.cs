using ClosedXML.Excel;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using SmartKostanay.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SmartKostanay.Services
{
    public class CadastreService
    {
        private readonly IMongoCollection<IzhsLandPlot> _plots;
        private readonly IMongoCollection<LandDocument> _documents;
        private readonly IMongoCollection<IzhsPhoto> _photos;
        private readonly IMongoCollection<IzhsAuditLog> _auditLogs;

        public CadastreService(IMongoClient client, string databaseName)
        {
            var database = client.GetDatabase(databaseName);
            _plots = database.GetCollection<IzhsLandPlot>("IzhsLandPlots");
            _documents = database.GetCollection<LandDocument>("IzhsDocuments");
            _photos = database.GetCollection<IzhsPhoto>("IzhsPhotos");
            _auditLogs = database.GetCollection<IzhsAuditLog>("IzhsAuditLog");
        }

        #region Методы для Участков (IzhsLandPlots)

        public async Task<IzhsLandPlot> GetByCadNumberAsync(string cadNumber)
        {
            return await _plots.Find(x => x.CadastralNumber == cadNumber).FirstOrDefaultAsync();
        }

        public async Task<IzhsLandPlot?> GetByIdAsync(string id)
        {
            if (!ObjectId.TryParse(id, out _)) return null;
            return await _plots.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        // Обновленный фильтр с поиском по ИИН и Телефону
        public async Task<(List<IzhsLandPlot> Items, long TotalCount)> GetFilteredAsync(
            string? district, string? status, int? stage, string? searchTerm, int page, int pageSize)
        {
            var builder = Builders<IzhsLandPlot>.Filter;
            var filter = builder.Empty;

            if (!string.IsNullOrEmpty(district))
                filter &= builder.Eq(x => x.District, district);

            if (!string.IsNullOrEmpty(status))
                filter &= builder.Eq(x => x.OverallStatus, status);

            if (stage.HasValue)
                filter &= builder.Eq(x => x.CurrentStage, stage.Value);

            // Поиск по ключевому слову
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var searchFilter = builder.Or(
                    builder.Regex(x => x.CadastralNumber, new BsonRegularExpression(searchTerm, "i")),
                    builder.Regex(x => x.Owner.IdentityNumber, new BsonRegularExpression(searchTerm, "i")),
                    builder.Regex(x => x.Owner.PhoneNumber, new BsonRegularExpression(searchTerm, "i"))
                );
                filter &= searchFilter;
            }

            var totalCount = await _plots.CountDocumentsAsync(filter);
            int skip = (page - 1) * pageSize;
            if (skip < 0) skip = 0;

            var data = await _plots.Find(filter)
                                   .Skip(skip)
                                   .Limit(pageSize)
                                   .ToListAsync();

            return (data, totalCount);
        }

        public async Task CreateAsync(IzhsLandPlot plot)
        {
            DateTime baseDate = plot.IssueDate ?? DateTime.UtcNow;
            plot.DateOfCreation = DateTime.UtcNow;
            plot.ModifiedOn = DateTime.UtcNow;
            plot.OverallStatus = "ACTIVE";

            plot.Stages = new List<IzhsStage>
            {
                new IzhsStage { StageNumber = 1, Name = "Получение АПЗ и эскизного проекта", Status = "PENDING", Deadline = baseDate.AddMonths(3) },
                new IzhsStage { StageNumber = 2, Name = "Ограждение территории и начало строительных работ", Status = "PENDING", Deadline = baseDate.AddMonths(6) },
                new IzhsStage { StageNumber = 3, Name = "Ежегодная фиксация степени освоения", Status = "PENDING", Deadline = new DateTime(DateTime.Now.Year, 10, 31) }
            };

            await _plots.InsertOneAsync(plot);
        }

        public async Task<bool> UpdateAsync(string id, IzhsLandPlot updatedPlot)
        {
            updatedPlot.ModifiedOn = DateTime.UtcNow;
            var result = await _plots.ReplaceOneAsync(x => x.Id == id, updatedPlot);
            return result.IsAcknowledged && result.ModifiedCount > 0;
        }

        public async Task<bool> ExcludeAsync(string id, string reason)
        {
            if (!ObjectId.TryParse(id, out _)) return false;

            var update = Builders<IzhsLandPlot>.Update
                .Set(x => x.OverallStatus, "EXCLUDED")
                .Set(x => x.ExcludedFromControl, true)
                .Set(x => x.ExclusionReason, reason)
                .Set(x => x.ExclusionDate, DateTime.UtcNow)
                .Set(x => x.ModifiedOn, DateTime.UtcNow);

            var result = await _plots.UpdateOneAsync(x => x.Id == id, update);
            return result.ModifiedCount > 0;
        }

        #endregion

        #region Методы для Документов (IzhsDocuments) — ВЕРНУЛ НА МЕСТО

        public async Task SaveDocumentAsync(LandDocument document)
        {
            await _documents.InsertOneAsync(document);
        }

        public async Task<List<LandDocument>> GetDocumentsByPlotIdAsync(string plotId)
        {
            return await _documents.Find(d => d.LandPlotId == plotId).ToListAsync();
        }

        public async Task<LandDocument?> GetDocumentByIdAsync(string docId)
        {
            if (!ObjectId.TryParse(docId, out _)) return null;
            return await _documents.Find(d => d.Id == docId).FirstOrDefaultAsync();
        }

        public async Task<bool> DeleteDocumentAsync(string docId)
        {
            if (!ObjectId.TryParse(docId, out _)) return false;
            var result = await _documents.DeleteOneAsync(d => d.Id == docId);
            return result.DeletedCount > 0;
        }

        #endregion

        #region Геоданные и Экспорт

        public async Task<object> GetMapDataAsync(string? district, string? status)
        {
            var filterBuilder = Builders<IzhsLandPlot>.Filter;
            var filter = filterBuilder.Eq(p => p.ExcludedFromControl, false);

            if (!string.IsNullOrEmpty(district)) filter &= filterBuilder.Eq(p => p.District, district);
            if (!string.IsNullOrEmpty(status)) filter &= filterBuilder.Eq(p => p.OverallStatus, status);

            var plots = await _plots.Find(filter).ToListAsync();

            return new
            {
                type = "FeatureCollection",
                features = plots.Select(p => new
                {
                    type = "Feature",
                    id = p.Id,
                    geometry = new
                    {
                        type = "Point",
                        coordinates = new double[] { p.Location.Coordinates.Values[0], p.Location.Coordinates.Values[1] }
                    },
                    properties = new
                    {
                        p.CadastralNumber,
                        p.Address,
                        p.OverallStatus,
                        p.Area,
                        ownerName = p.Owner != null ? $"{p.Owner.Lastname} {p.Owner.Firstname}" : "Нет данных"
                    }
                })
            };
        }

        public async Task<byte[]> ExportToExcelAsync(string? district, string? status)
        {
            var filterBuilder = Builders<IzhsLandPlot>.Filter;
            var filter = filterBuilder.Empty;

            if (!string.IsNullOrEmpty(district)) filter &= filterBuilder.Eq(p => p.District, district);
            if (!string.IsNullOrEmpty(status)) filter &= filterBuilder.Eq(p => p.OverallStatus, status);

            var plots = await _plots.Find(filter).ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Отчет по участкам");
                var headers = new string[] { "№", "Кадастровый номер", "Адрес", "Район", "Площадь (га)", "Владелец", "ИИН", "Телефон", "Статус" };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                for (int i = 0; i < plots.Count; i++)
                {
                    var row = i + 2;
                    var plot = plots[i];
                    worksheet.Cell(row, 1).Value = i + 1;
                    worksheet.Cell(row, 2).Value = plot.CadastralNumber;
                    worksheet.Cell(row, 3).Value = plot.Address;
                    worksheet.Cell(row, 4).Value = plot.District;
                    worksheet.Cell(row, 5).Value = plot.Area;
                    worksheet.Cell(row, 6).Value = plot.Owner != null ? $"{plot.Owner.Lastname} {plot.Owner.Firstname} {plot.Owner.Patronymic}" : "Нет данных";
                    worksheet.Cell(row, 7).Value = plot.Owner?.IdentityNumber ?? "-";
                    worksheet.Cell(row, 8).Value = plot.Owner?.PhoneNumber ?? "-";
                    worksheet.Cell(row, 9).Value = plot.OverallStatus;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }
        #endregion

        #region ФОТО
        public async Task<IzhsPhoto?> GetPhotoByIdAsync(string photoId)
        {
            if (!ObjectId.TryParse(photoId, out _)) return null;
            return await _photos.Find(p => p.Id == photoId).FirstOrDefaultAsync();
        }

        public async Task<List<IzhsPhoto>> GetPhotosByPlotIdAsync(string plotId)
        {
            return await _photos.Find(p => p.LandPlotId == plotId).ToListAsync();
        }

        public async Task SavePhotoAsync(IzhsPhoto photo)
        {
            await _photos.InsertOneAsync(photo);
        }

        public async Task<bool> DeletePhotoAsync(string photoId)
        {
            var result = await _photos.DeleteOneAsync(p => p.Id == photoId);
            return result.DeletedCount > 0;
        }

        public async Task<bool> IsPointInPlotBoundary(string plotId, double lon, double lat)
        {
            var plot = await GetByIdAsync(plotId);
            if (plot?.Boundary == null) return false;

            var point = GeoJson.Point(GeoJson.Geographic(lon, lat));
            var filter = Builders<IzhsLandPlot>.Filter.And(
                Builders<IzhsLandPlot>.Filter.Eq(x => x.Id, plotId),
                Builders<IzhsLandPlot>.Filter.GeoWithin(x => x.Boundary, point)
            );

            return await _plots.Find(filter).AnyAsync();
        }

        #endregion

        public async Task<List<IzhsAuditLog>> GetAuditLogByPlotIdAsync(string landPlotId)
        {
            var filter = Builders<IzhsAuditLog>.Filter.And(
                Builders<IzhsAuditLog>.Filter.Eq(x => x.EntityId, landPlotId),
                Builders<IzhsAuditLog>.Filter.Eq(x => x.EntityType, "LAND_PLOT")
            );

            return await _auditLogs
                .Find(filter)
                .SortByDescending(x => x.Timestamp)
                .ToListAsync();
        }
    }
}