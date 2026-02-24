using AutoMapper;
using OpenGate.Application.DTOs;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Application.Services;

public class TicketService(ITicketRepository repository, IMapper mapper) : ITicketService
{
    public async Task<TicketDto?> GetByIdAsync(string id)
    {
        var entity = await repository.GetByIdAsync(id);
        return entity == null ? null : mapper.Map<TicketDto>(entity);
    }

    public async Task<IEnumerable<TicketDto>> GetAllAsync()
    {
        var entities = await repository.GetAllAsync();
        return mapper.Map<IEnumerable<TicketDto>>(entities);
    }

    public async Task<IEnumerable<TicketDto>> GetByUserAsync(string userId)
    {
        var entities = await repository.GetByUserAsync(userId);
        return mapper.Map<IEnumerable<TicketDto>>(entities);
    }

    public async Task<TicketDto> CreateAsync(CreateTicketDto dto)
    {
        var entity = mapper.Map<Ticket>(dto);

        if (!string.IsNullOrWhiteSpace(dto.Body))
        {
            entity.Messages.Add(new TicketMessage
            {
                SenderId = dto.UserId,
                SenderName = "Customer",
                IsStaff = false,
                Body = dto.Body,
                CreatedAt = DateTime.UtcNow
            });
        }

        var created = await repository.CreateAsync(entity);
        return mapper.Map<TicketDto>(created);
    }

    public async Task<TicketDto?> UpdateAsync(string id, CreateTicketDto dto)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null) return null;

        entity.Subject = dto.Subject;
        entity.Priority = dto.Priority;
        entity.OrderId = dto.OrderId;
        entity.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(entity);
        return mapper.Map<TicketDto>(entity);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null) return false;

        await repository.DeleteAsync(id);
        return true;
    }

    public async Task<TicketMessageDto?> AddMessageAsync(string ticketId, CreateTicketMessageDto dto)
    {
        var ticket = await repository.GetByIdAsync(ticketId);
        if (ticket == null) return null;

        var message = new TicketMessage
        {
            SenderId = dto.SenderId,
            SenderName = dto.SenderName,
            IsStaff = dto.IsStaff,
            Body = dto.Body,
            CreatedAt = DateTime.UtcNow
        };

        ticket.Messages.Add(message);
        ticket.Status = dto.IsStaff ? TicketStatus.Answered : TicketStatus.CustomerReply;
        ticket.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(ticket);

        return mapper.Map<TicketMessageDto>(message);
    }

    public async Task<TicketDto?> CloseTicketAsync(string id)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null) return null;

        entity.Status = TicketStatus.Closed;
        entity.ClosedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(entity);
        return mapper.Map<TicketDto>(entity);
    }
}
