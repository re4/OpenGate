using MongoDB.Driver;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;
using OpenGate.Infrastructure.Data;

namespace OpenGate.Infrastructure.Repositories;

public class TicketRepository(MongoDbContext context) : MongoRepository<Ticket>(context, context.Tickets), ITicketRepository
{

    public async Task<IEnumerable<Ticket>> GetByUserAsync(string userId)
    {
        return await Collection.Find(t => t.UserId == userId).ToListAsync();
    }

    public async Task<IEnumerable<Ticket>> GetByStatusAsync(TicketStatus status)
    {
        return await Collection.Find(t => t.Status == status).ToListAsync();
    }

    public async Task<IEnumerable<Ticket>> GetOpenTicketsAsync()
    {
        return await Collection.Find(t => t.Status != TicketStatus.Closed).ToListAsync();
    }
}
