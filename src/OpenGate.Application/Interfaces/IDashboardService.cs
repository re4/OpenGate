using OpenGate.Application.DTOs;

namespace OpenGate.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync();
}
