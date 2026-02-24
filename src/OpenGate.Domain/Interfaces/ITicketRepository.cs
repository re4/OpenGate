using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;

namespace OpenGate.Domain.Interfaces;

public interface ITicketRepository : IRepository<Ticket>
{
    Task<IEnumerable<Ticket>> GetByUserAsync(string userId);
    Task<IEnumerable<Ticket>> GetByStatusAsync(TicketStatus status);
    Task<IEnumerable<Ticket>> GetOpenTicketsAsync();
}
