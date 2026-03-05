using AutoMapper;
using OpenGate.Application.DTOs;
using OpenGate.Domain.Entities;

namespace OpenGate.Application.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Category
        CreateMap<Category, CategoryDto>();
        CreateMap<CreateCategoryDto, Category>();
        CreateMap<UpdateCategoryDto, Category>();

        // Product
        CreateMap<ConfigurableOptionValue, ConfigurableOptionValueDto>();
        CreateMap<ConfigurableOptionValueDto, ConfigurableOptionValue>();
        CreateMap<ConfigurableOption, ConfigurableOptionDto>();
        CreateMap<ConfigurableOptionDto, ConfigurableOption>();
        CreateMap<Product, ProductDto>();
        CreateMap<CreateProductDto, Product>();
        CreateMap<UpdateProductDto, Product>();

        // Order
        CreateMap<Order, OrderDto>();
        CreateMap<OrderItem, OrderItemDto>();
        CreateMap<OrderItemDto, OrderItem>();

        // Invoice
        CreateMap<Invoice, InvoiceDto>();
        CreateMap<InvoiceLine, InvoiceLineDto>();
        CreateMap<InvoiceLineDto, InvoiceLine>();
        CreateMap<CreateInvoiceDto, Invoice>()
            .ForMember(dest => dest.Status, opt => opt.Ignore())
            .ForMember(dest => dest.PaidAt, opt => opt.Ignore());

        // Ticket
        CreateMap<Ticket, TicketDto>();
        CreateMap<TicketMessage, TicketMessageDto>();
        CreateMap<TicketMessageDto, TicketMessage>();
        CreateMap<TicketAttachment, TicketAttachmentDto>();
        CreateMap<TicketAttachmentDto, TicketAttachment>();
        CreateMap<CreateTicketDto, Ticket>()
            .ForMember(dest => dest.Messages, opt => opt.Ignore());
        CreateMap<CreateTicketMessageDto, TicketMessage>();

        // Payment
        CreateMap<Payment, PaymentDto>();
        CreateMap<CreatePaymentDto, Payment>();

        // Setting
        CreateMap<Setting, SettingDto>();
        CreateMap<UpdateSettingDto, Setting>();

        // Theme
        CreateMap<Theme, ThemeDto>();
        CreateMap<CreateThemeDto, Theme>();
    }
}
