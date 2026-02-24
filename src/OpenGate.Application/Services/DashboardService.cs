using AutoMapper;
using OpenGate.Application.DTOs;
using OpenGate.Application.Interfaces;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Application.Services;

public class DashboardService(
    IOrderRepository orderRepository,
    ITicketRepository ticketRepository,
    IMapper mapper) : IDashboardService
{
    public async Task<DashboardStatsDto> GetStatsAsync()
    {
        var totalRevenue = await orderRepository.GetTotalRevenueAsync();
        var allOrders = await orderRepository.GetAllAsync();
        var ordersList = allOrders.ToList();
        var activeOrders = await orderRepository.GetByStatusAsync(OrderStatus.Active);
        var openTickets = await ticketRepository.GetOpenTicketsAsync();

        var recentOrders = ordersList
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .ToList();

        var monthlyRevenue = new List<MonthlyRevenueDto>();
        var startOfYear = new DateTime(DateTime.UtcNow.Year, 1, 1);
        for (var month = 1; month <= 12; month++)
        {
            var monthStart = new DateTime(DateTime.UtcNow.Year, month, 1);
            var monthEnd = monthStart.AddMonths(1);
            var amount = await orderRepository.GetTotalRevenueAsync(monthStart, monthEnd);
            monthlyRevenue.Add(new MonthlyRevenueDto
            {
                Month = monthStart.ToString("yyyy-MM"),
                Amount = amount
            });
        }

        return new DashboardStatsDto
        {
            TotalRevenue = totalRevenue,
            TotalOrders = ordersList.Count,
            ActiveOrders = activeOrders.Count(),
            TotalUsers = 0, // Requires IUserRepository - integrate when user store is available
            OpenTickets = openTickets.Count(),
            RecentOrders = mapper.Map<List<OrderDto>>(recentOrders),
            MonthlyRevenue = monthlyRevenue
        };
    }
}
