namespace OpenGate.Application.DTOs;

public class DashboardStatsDto
{
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int ActiveOrders { get; set; }
    public long TotalUsers { get; set; }
    public int OpenTickets { get; set; }
    public List<OrderDto> RecentOrders { get; set; } = new();
    public List<MonthlyRevenueDto> MonthlyRevenue { get; set; } = new();
}

public class MonthlyRevenueDto
{
    public string Month { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
