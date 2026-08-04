using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Dairy.DMO;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dairy.Context
{
    public class DairyRepository
    {
        private readonly IMongoCollection<DairyProduct>? _dairyProducts;

        private static MongoClient CreateClient(string connStr)
        {
            try
            {
                var settings = MongoClientSettings.FromConnectionString(connStr);
                settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
                settings.ConnectTimeout = TimeSpan.FromSeconds(2);
                settings.SocketTimeout = TimeSpan.FromSeconds(2);
                settings.SslSettings = new SslSettings
                {
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                };
                return new MongoClient(settings);
            }
            catch
            {
                return new MongoClient(connStr);
            }
        }

        public DairyRepository(string connectionString, string databaseName)
        {
            try
            {
                var client = CreateClient(connectionString);
                var database = client.GetDatabase(databaseName);
                _dairyProducts = database.GetCollection<DairyProduct>("dairyProducts");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DAIRY REPO INIT WARNING] {ex.Message}");
            }
        }

        public async Task<List<DairyProduct>> GetAllProductsAsync()
        {
            if (_dairyProducts == null) return new List<DairyProduct>();
            try
            {
                using var cts = new CancellationTokenSource(1500);
                return await _dairyProducts.Find(_ => true).ToListAsync(cts.Token);
            }
            catch
            {
                return new List<DairyProduct>();
            }
        }

        public async Task<DairyProduct?> GetProductAsync(string id)
        {
            if (ObjectId.TryParse(id, out var oid))
            {
                if (_dairyProducts != null)
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(1500);
                        return await _dairyProducts.Find(x => x.Id == oid).FirstOrDefaultAsync(cts.Token);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DAIRY REPO GET ERR] {ex.Message}");
                    }
                }
            }
            return null;
        }

        public async Task AddProductAsync(DairyProduct product)
        {
            if (_dairyProducts != null)
            {
                try
                {
                    using var cts = new CancellationTokenSource(1500);
                    await _dairyProducts.InsertOneAsync(product, cancellationToken: cts.Token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DAIRY REPO ADD ERR] {ex.Message}");
                }
            }
        }

        public async Task UpdateProductAsync(string id, DairyProduct product)
        {
            if (ObjectId.TryParse(id, out var oid))
            {
                product.Id = oid;
                if (_dairyProducts != null)
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(1500);
                        await _dairyProducts.ReplaceOneAsync(p => p.Id == oid, product, cancellationToken: cts.Token);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DAIRY REPO UPDATE ERR] {ex.Message}");
                    }
                }
            }
        }

        public async Task DeleteProductAsync(string id)
        {
            if (ObjectId.TryParse(id, out var oid))
            {
                if (_dairyProducts != null)
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(1500);
                        await _dairyProducts.DeleteOneAsync(p => p.Id == oid, cancellationToken: cts.Token);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DAIRY REPO DELETE ERR] {ex.Message}");
                    }
                }
            }
        }
    }
}
