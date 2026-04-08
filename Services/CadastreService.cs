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
    }
}