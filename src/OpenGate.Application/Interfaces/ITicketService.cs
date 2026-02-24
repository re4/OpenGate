using OpenGate.Application.DTOs;

namespace OpenGate.Application.Interfaces;

public interface ITicketService
{
    Task<TicketDto?> GetByIdAsync(string id);
    Task<IEnumerable<TicketDto>> GetAllAsync();
    Task<IEnumerable<TicketDto>> GetByUserAsync(string userId);
    Task<TicketDto> CreateAsync(CreateTicketDto dto);
    Task<TicketDto?> UpdateAsync(string id, CreateTicketDto dto);
    Task<bool> DeleteAsync(string id);
    Task<TicketMessageDto?> AddMessageAsync(string ticketId, CreateTicketMessageDto dto);
    Task<TicketDto?> CloseTicketAsync(string id);
}
