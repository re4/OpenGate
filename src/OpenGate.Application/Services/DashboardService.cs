using AutoMapper;
using OpenGate.Application.DTOs;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Application.Services;

/// <summary>
/// Aggregates dashboard statistics for the admin overview page. All
/// expensive operations are pushed into the database to avoid streaming
/// entire collections through the application process.
/// </summary>
public class DashboardService(
    IOrderRepository orderRepository,
    ITicketRepository ticketRepository,
    IMapper mapper) : IDashboardService
{
    /// <summary>
    /// Builds the dashboard statistics with constant-memory database
    /// aggregations and a single concurrent fan-out so the page load is
    /// dominated by the slowest individual query.
    /// </summary>
    public async Task<DashboardStatsDto> GetStatsAsync()
    {
        var year = DateTime.UtcNow.Year;

        var revenueTask = orderRepository.GetTotalRevenueAsync();
        var statusCountsTask = orderRepository.GetStatusCountsAsync();
        var openTicketsTask = ticketRepository.GetOpenTicketsAsync();
        var recentTask = orderRepository.GetRecentAsync(10);
        var monthlyTask = orderRepository.GetMonthlyRevenueAsync(year);

        await Task.WhenAll(revenueTask, statusCountsTask, openTicketsTask, recentTask, monthlyTask);

        var statusCounts = await statusCountsTask;
        long totalOrders = 0;
        foreach (var pair in statusCounts) totalOrders += pair.Value;
        statusCounts.TryGetValue(OrderStatus.Active, out var activeOrders);

        var monthlyRevenue = (await monthlyTask)
            .Select(b => new MonthlyRevenueDto
            {
                Month = new DateTime(year, b.Month, 1).ToString("yyyy-MM"),
                Amount = b.Total
            })
            .ToList();

        return new DashboardStatsDto
        {
            TotalRevenue = await revenueTask,
            TotalOrders = (int)Math.Min(int.MaxValue, totalOrders),
            ActiveOrders = (int)Math.Min(int.MaxValue, activeOrders),
            TotalUsers = 0,
            OpenTickets = (await openTicketsTask).Count(),
            RecentOrders = mapper.Map<List<OrderDto>>((await recentTask).ToList()),
            MonthlyRevenue = monthlyRevenue
        };
    }
}
