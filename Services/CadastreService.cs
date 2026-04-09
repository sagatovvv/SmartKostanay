using MongoDB.Driver;
using SmartKostanay.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartKostanay.Services
{
    public class CadastreService
    {
        private readonly IMongoCollection<IzhsLandPlot> _plots;

   
        public CadastreService(IMongoClient client, string databaseName)
        {
            var database = client.GetDatabase(databaseName);
            _plots = database.GetCollection<IzhsLandPlot>("IzhsLandPlots");
        }

        public async Task<IzhsLandPlot> GetByCadNumberAsync(string cadNumber)
        {
            return await _plots.Find(x => x.CadastralNumber == cadNumber).FirstOrDefaultAsync();
        }

        public async Task<IzhsLandPlot?> GetByIdAsync(string id)
        {
            // Проверяем, является ли переданная строка валидным ObjectId для MongoDB
            if (!MongoDB.Bson.ObjectId.TryParse(id, out var objectId))
            {
                return null;
            }

            return await _plots
                .Find(x => x.Id == id)
                .FirstOrDefaultAsync();
        }


        public async Task<(List<IzhsLandPlot> Items, long TotalCount)> GetFilteredAsync(
            string district, string status, int? stage, int page, int pageSize)
        {
            var builder = Builders<IzhsLandPlot>.Filter;
            var filter = builder.Empty;

            if (!string.IsNullOrEmpty(district))
                filter &= builder.Eq(x => x.District, district);

            if (!string.IsNullOrEmpty(status))
                filter &= builder.Eq(x => x.OverallStatus, status);

            if (stage.HasValue)
                filter &= builder.Eq(x => x.CurrentStage, stage.Value);

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
            // Если дата выдачи не пришла, используем текущую (защита от ошибок)
            DateTime baseDate = plot.IssueDate ?? DateTime.UtcNow;

            plot.DateOfCreation = DateTime.UtcNow;
            plot.ModifiedOn = DateTime.UtcNow;
            plot.OverallStatus = "ACTIVE";

            plot.Stages = new List<IzhsStage>
            {
                new IzhsStage {
                    StageNumber = 1,
                    Name = "Получение АПЗ и эскизного проекта",
                    Status = "PENDING",
                    Deadline = baseDate.AddMonths(3) 
                },
                new IzhsStage {
                    StageNumber = 2,
                    Name = "Ограждение территории и начало строительных работ",
                    Status = "PENDING",
                    Deadline = baseDate.AddMonths(6)
                },
                new IzhsStage {
                    StageNumber = 3,
                    Name = "Ежегодная фиксация степени освоения",
                    Status = "PENDING",
                    Deadline = new DateTime(DateTime.Now.Year, 10, 31)
                }
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
            if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            {
                return false;
            }

            var update = Builders<IzhsLandPlot>.Update
                .Set(x => x.OverallStatus, "EXCLUDED")    // Статус становится "Исключен"
                .Set(x => x.ExcludedFromControl, true)   // Ставим метку, что контроль снят
                .Set(x => x.ExclusionReason, reason)     // Сохраняем причину из ТЗ
                .Set(x => x.ExclusionDate, DateTime.UtcNow) // Дата исключения
                .Set(x => x.ModifiedOn, DateTime.UtcNow);   // Дата последнего изменения

            var result = await _plots.UpdateOneAsync(x => x.Id == id, update);

            return result.ModifiedCount > 0;
        }


    }
}