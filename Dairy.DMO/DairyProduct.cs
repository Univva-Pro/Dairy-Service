using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dairy.DMO
{
    public class DairyProduct
    {
        [BsonId]
        public ObjectId Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public int StockQuantity { get; set; }
        public double FatContentPercentage { get; set; }
        public double Price { get; set; }
        public DateTime PasteurizationDate { get; set; }
        public string StorageTemperatureRange { get; set; } = string.Empty;
    }
}
