using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OpenGate.Domain.Entities;

namespace OpenGate.Infrastructure.Data;

public class MongoDbContext(IOptions<MongoDbSettings> settings)
{
    private readonly IMongoDatabase _database = new MongoClient(settings.Value.ConnectionString).GetDatabase(settings.Value.DatabaseName);

    public IMongoCollection<T> GetCollection<T>(string name) where T : BaseEntity
    {
        return _database.GetCollection<T>(name);
    }

    public IMongoCollection<Product> Products => GetCollection<Product>("Products");
    public IMongoCollection<Category> Categories => GetCollection<Category>("Categories");
    public IMongoCollection<Order> Orders => GetCollection<Order>("Orders");
    public IMongoCollection<Invoice> Invoices => GetCollection<Invoice>("Invoices");
    public IMongoCollection<Payment> Payments => GetCollection<Payment>("Payments");
    public IMongoCollection<Ticket> Tickets => GetCollection<Ticket>("Tickets");
    public IMongoCollection<Setting> Settings => GetCollection<Setting>("Settings");
    public IMongoCollection<ExtensionConfig> ExtensionConfigs => GetCollection<ExtensionConfig>("ExtensionConfigs");
}
