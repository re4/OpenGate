using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenGate.Application.Interfaces;
using OpenGate.Application.Mapping;
using OpenGate.Application.Services;
using OpenGate.Domain.Interfaces;
using OpenGate.Infrastructure.Data;
using OpenGate.Infrastructure.Repositories;

namespace OpenGate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDB"));
        services.AddSingleton<MongoDbContext>();

        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ISettingRepository, SettingRepository>();
        services.AddScoped<IExtensionConfigRepository, ExtensionConfigRepository>();

        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<ISettingService, SettingService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IServerManagementService, ServerManagementService>();

        services.AddAutoMapper(cfg => { }, typeof(MappingProfile).Assembly);

        return services;
    }
}
