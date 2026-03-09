using Microsoft.AspNetCore.Identity;
using MySqlConnector;
using OpenGate.Domain.Entities;
using OpenGate.Domain.Enums;
using OpenGate.Domain.Interfaces;

namespace OpenGate.Application.Migration;

public class PaymenterMigrationService : IMigrationService
{
    private readonly ICategoryRepository _categoryRepo;
    private readonly IProductRepository _productRepo;
    private readonly IOrderRepository _orderRepo;
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly IPaymentRepository _paymentRepo;
    private readonly ITicketRepository _ticketRepo;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    private MigrationProgress _progress = new();
    private readonly object _lock = new();

    public PaymenterMigrationService(
        ICategoryRepository categoryRepo,
        IProductRepository productRepo,
        IOrderRepository orderRepo,
        IInvoiceRepository invoiceRepo,
        IPaymentRepository paymentRepo,
        ITicketRepository ticketRepo,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _categoryRepo = categoryRepo;
        _productRepo = productRepo;
        _orderRepo = orderRepo;
        _invoiceRepo = invoiceRepo;
        _paymentRepo = paymentRepo;
        _ticketRepo = ticketRepo;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public MigrationProgress GetProgress()
    {
        lock (_lock) { return _progress; }
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(string connectionString)
    {
        try
        {
            await using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();

            var counts = new PaymenterCounts
            {
                Users = await CountTable(conn, "users"),
                Categories = await CountTable(conn, "categories"),
                Products = await CountTable(conn, "products"),
                Orders = await CountTable(conn, "orders"),
                Services = await CountTable(conn, "services"),
                Invoices = await CountTable(conn, "invoices"),
                InvoiceTransactions = await CountTable(conn, "invoice_transactions"),
                Tickets = await CountTable(conn, "tickets"),
                TicketMessages = await CountTable(conn, "ticket_messages"),
            };

            return new ConnectionTestResult { Success = true, Counts = counts };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<MigrationProgress> RunMigrationAsync(string connectionString)
    {
        lock (_lock)
        {
            if (_progress.IsRunning)
                return _progress;

            _progress = new MigrationProgress { IsRunning = true };
        }

        try
        {
            await using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();

            var categoryMap = await MigrateCategories(conn);
            var userMap = await MigrateUsers(conn);
            var (productMap, planMap) = await MigrateProducts(conn, categoryMap);
            var (orderMap, serviceOrderMap) = await MigrateOrders(conn, userMap, productMap, planMap);
            await MigrateInvoices(conn, userMap, serviceOrderMap);
            await MigratePayments(conn, userMap, conn);
            await MigrateTickets(conn, userMap, serviceOrderMap);

            lock (_lock)
            {
                _progress.IsRunning = false;
                _progress.IsComplete = true;
            }
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _progress.IsRunning = false;
                _progress.Error = ex.Message;
            }
        }

        return _progress;
    }

    private async Task<Dictionary<long, string>> MigrateCategories(MySqlConnection conn)
    {
        SetStep("Categories");
        var map = new Dictionary<long, string>();
        var step = new MigrationStepResult { Name = "Categories" };

        var categories = await ReadAll<PmCategory>(conn,
            "SELECT id, slug, name, description, image, parent_id, sort, created_at, updated_at FROM categories");

        foreach (var pm in categories)
        {
            try
            {
                var cat = new Category
                {
                    Name = pm.Name,
                    Description = pm.Description,
                    Slug = pm.Slug,
                    SortOrder = pm.Sort ?? 0,
                    IsActive = true,
                    CreatedAt = pm.CreatedAt ?? DateTime.UtcNow,
                    UpdatedAt = pm.UpdatedAt ?? DateTime.UtcNow
                };
                var created = await _categoryRepo.CreateAsync(cat);
                map[pm.Id] = created.Id;
                step.Imported++;
            }
            catch (Exception ex)
            {
                step.Failed++;
                step.Warnings.Add($"Category {pm.Id} '{pm.Name}': {ex.Message}");
            }
        }

        step.Success = step.Failed == 0;
        CompleteStep(step);
        return map;
    }

    private async Task<Dictionary<long, string>> MigrateUsers(MySqlConnection conn)
    {
        SetStep("Users");
        var map = new Dictionary<long, string>();
        var step = new MigrationStepResult { Name = "Users" };

        if (!await _roleManager.RoleExistsAsync("Admin"))
            await _roleManager.CreateAsync(new ApplicationRole { Name = "Admin" });

        var users = await ReadAll<PmUser>(conn,
            "SELECT id, first_name, last_name, email, password, role_id, email_verified_at, created_at, updated_at FROM users");

        foreach (var pm in users)
        {
            try
            {
                var existing = await _userManager.FindByEmailAsync(pm.Email);
                if (existing != null)
                {
                    map[pm.Id] = existing.Id.ToString();
                    step.Skipped++;
                    continue;
                }

                var user = new ApplicationUser
                {
                    UserName = pm.Email,
                    Email = pm.Email,
                    EmailConfirmed = pm.EmailVerifiedAt != null,
                    FirstName = pm.FirstName ?? string.Empty,
                    LastName = pm.LastName ?? string.Empty,
                    CreatedAt = pm.CreatedAt ?? DateTime.UtcNow,
                    PasswordHash = pm.Password
                };

                var result = await _userManager.CreateAsync(user);
                if (!result.Succeeded)
                {
                    step.Failed++;
                    step.Warnings.Add($"User {pm.Id} '{pm.Email}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    continue;
                }

                if (pm.RoleId != null)
                    await _userManager.AddToRoleAsync(user, "Admin");

                map[pm.Id] = user.Id.ToString();
                step.Imported++;
            }
            catch (Exception ex)
            {
                step.Failed++;
                step.Warnings.Add($"User {pm.Id} '{pm.Email}': {ex.Message}");
            }
        }

        step.Success = step.Failed == 0;
        CompleteStep(step);
        return map;
    }

    private async Task<(Dictionary<long, string> productMap, Dictionary<long, PmPlan> planMap)> MigrateProducts(
        MySqlConnection conn, Dictionary<long, string> categoryMap)
    {
        SetStep("Products");
        var productMap = new Dictionary<long, string>();
        var planMap = new Dictionary<long, PmPlan>();
        var step = new MigrationStepResult { Name = "Products" };

        var products = await ReadAll<PmProduct>(conn,
            "SELECT id, category_id, name, image, slug, description, stock, server_id, sort, created_at, updated_at FROM products");
        var plans = await ReadAll<PmPlan>(conn,
            "SELECT id, name, priceable_type, priceable_id, type, billing_period, billing_unit, sort FROM plans");
        var prices = await ReadAll<PmPrice>(conn,
            "SELECT id, plan_id, currency_code, price, setup_fee FROM prices");

        var configOptions = await ReadAll<PmConfigOption>(conn,
            "SELECT id, parent_id, name, sort, hidden, description FROM config_options");
        var configOptionProducts = await ReadAll<PmConfigOptionProduct>(conn,
            "SELECT id, product_id, config_option_id FROM config_option_products");

        var plansByProduct = plans
            .Where(p => p.PriceableType.Contains("Product", StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => p.PriceableId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var pricesByPlan = prices.GroupBy(p => p.PlanId).ToDictionary(g => g.Key, g => g.ToList());

        var parentOptions = configOptions.Where(co => co.ParentId == null).ToList();
        var childOptions = configOptions.Where(co => co.ParentId != null).GroupBy(co => co.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
        var copByProduct = configOptionProducts.GroupBy(cop => cop.ProductId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ConfigOptionId).ToHashSet());

        foreach (var plan in plans)
            planMap[plan.Id] = plan;

        foreach (var pm in products)
        {
            try
            {
                if (!categoryMap.TryGetValue(pm.CategoryId, out var catId))
                {
                    step.Failed++;
                    step.Warnings.Add($"Product {pm.Id} '{pm.Name}': category {pm.CategoryId} not found");
                    continue;
                }

                decimal price = 0;
                decimal? setupFee = null;
                var billingCycle = BillingCycle.Monthly;

                if (plansByProduct.TryGetValue(pm.Id, out var productPlans) && productPlans.Count > 0)
                {
                    var plan = productPlans.OrderBy(p => p.Sort ?? 999).First();
                    billingCycle = MapBillingCycle(plan.Type, plan.BillingPeriod, plan.BillingUnit);

                    if (pricesByPlan.TryGetValue(plan.Id, out var planPrices) && planPrices.Count > 0)
                    {
                        var priceEntry = planPrices.First();
                        price = priceEntry.Price;
                        setupFee = priceEntry.SetupFee;
                    }
                }

                var configurableOptions = new List<ConfigurableOption>();
                if (copByProduct.TryGetValue(pm.Id, out var productConfigIds))
                {
                    foreach (var parentOpt in parentOptions.Where(po => productConfigIds.Contains(po.Id) && !po.Hidden))
                    {
                        var option = new ConfigurableOption
                        {
                            Name = parentOpt.Name,
                            EnvironmentVariable = parentOpt.Name.Replace(" ", "_").ToUpperInvariant(),
                            Values = new List<ConfigurableOptionValue>()
                        };

                        if (childOptions.TryGetValue(parentOpt.Id, out var children))
                        {
                            foreach (var child in children.OrderBy(c => c.Sort ?? 999))
                            {
                                option.Values.Add(new ConfigurableOptionValue
                                {
                                    Label = child.Name,
                                    Value = child.Name,
                                    PriceModifier = 0
                                });
                            }
                        }

                        configurableOptions.Add(option);
                    }
                }

                var product = new Product
                {
                    Name = pm.Name,
                    Description = pm.Description,
                    CategoryId = catId,
                    Price = price,
                    SetupFee = setupFee,
                    BillingCycle = billingCycle,
                    Stock = pm.Stock ?? -1,
                    IsActive = true,
                    ServerId = pm.ServerId?.ToString(),
                    ConfigurableOptions = configurableOptions,
                    CreatedAt = pm.CreatedAt ?? DateTime.UtcNow,
                    UpdatedAt = pm.UpdatedAt ?? DateTime.UtcNow
                };

                var created = await _productRepo.CreateAsync(product);
                productMap[pm.Id] = created.Id;
                step.Imported++;
            }
            catch (Exception ex)
            {
                step.Failed++;
                step.Warnings.Add($"Product {pm.Id} '{pm.Name}': {ex.Message}");
            }
        }

        step.Success = step.Failed == 0;
        CompleteStep(step);
        return (productMap, planMap);
    }

    private async Task<(Dictionary<long, string> orderMap, Dictionary<long, string> serviceOrderMap)> MigrateOrders(
        MySqlConnection conn, Dictionary<long, string> userMap,
        Dictionary<long, string> productMap, Dictionary<long, PmPlan> planMap)
    {
        SetStep("Orders");
        var orderMap = new Dictionary<long, string>();
        var serviceOrderMap = new Dictionary<long, string>();
        var step = new MigrationStepResult { Name = "Orders" };

        var orders = await ReadAll<PmOrder>(conn,
            "SELECT id, user_id, currency_code, created_at, updated_at FROM orders");
        var services = await ReadAll<PmService>(conn,
            "SELECT id, status, order_id, product_id, user_id, currency_code, quantity, price, plan_id, coupon_id, expires_at, subscription_id, created_at, updated_at FROM services");
        var products = await ReadAll<PmProduct>(conn,
            "SELECT id, category_id, name, image, slug, description, stock, server_id, sort, created_at, updated_at FROM products");

        var productNames = products.ToDictionary(p => p.Id, p => p.Name);
        var servicesByOrder = services.Where(s => s.OrderId != null).GroupBy(s => s.OrderId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var pm in orders)
        {
            try
            {
                if (!userMap.TryGetValue(pm.UserId, out var userId))
                {
                    step.Skipped++;
                    continue;
                }

                var orderServices = servicesByOrder.GetValueOrDefault(pm.Id) ?? new List<PmService>();

                var items = new List<OrderItem>();
                var orderStatus = OrderStatus.Pending;
                DateTime? nextDueDate = null;
                DateTime? suspendedAt = null;
                DateTime? cancelledAt = null;

                foreach (var svc in orderServices)
                {
                    serviceOrderMap[svc.Id] = string.Empty; // placeholder, filled after create

                    var productId = svc.ProductId != null && productMap.TryGetValue(svc.ProductId.Value, out var pid) ? pid : string.Empty;
                    var productName = svc.ProductId != null && productNames.TryGetValue(svc.ProductId.Value, out var pn) ? pn : "Unknown";

                    var billingCycle = BillingCycle.Monthly;
                    if (svc.PlanId != null && planMap.TryGetValue(svc.PlanId.Value, out var plan))
                        billingCycle = MapBillingCycle(plan.Type, plan.BillingPeriod, plan.BillingUnit);

                    items.Add(new OrderItem
                    {
                        ProductId = productId,
                        ProductName = productName,
                        Quantity = svc.Quantity,
                        UnitPrice = svc.Price,
                        Total = svc.Price * svc.Quantity,
                        BillingCycle = billingCycle
                    });

                    var svcStatus = MapOrderStatus(svc.Status);
                    if (svcStatus > orderStatus) orderStatus = svcStatus;
                    if (svc.ExpiresAt != null && (nextDueDate == null || svc.ExpiresAt < nextDueDate))
                        nextDueDate = svc.ExpiresAt;
                    if (svcStatus == OrderStatus.Suspended) suspendedAt ??= svc.UpdatedAt;
                    if (svcStatus == OrderStatus.Cancelled) cancelledAt ??= svc.UpdatedAt;
                }

                var subtotal = items.Sum(i => i.Total);

                var order = new Order
                {
                    UserId = userId,
                    Status = orderStatus,
                    Items = items,
                    Subtotal = subtotal,
                    Tax = 0,
                    Total = subtotal,
                    Currency = pm.CurrencyCode,
                    NextDueDate = nextDueDate,
                    SuspendedAt = suspendedAt,
                    CancelledAt = cancelledAt,
                    CreatedAt = pm.CreatedAt ?? DateTime.UtcNow,
                    UpdatedAt = pm.UpdatedAt ?? DateTime.UtcNow
                };

                var created = await _orderRepo.CreateAsync(order);
                orderMap[pm.Id] = created.Id;

                foreach (var svc in orderServices)
                    serviceOrderMap[svc.Id] = created.Id;

                step.Imported++;
            }
            catch (Exception ex)
            {
                step.Failed++;
                step.Warnings.Add($"Order {pm.Id}: {ex.Message}");
            }
        }

        step.Success = step.Failed == 0;
        CompleteStep(step);
        return (orderMap, serviceOrderMap);
    }

    private async Task MigrateInvoices(MySqlConnection conn,
        Dictionary<long, string> userMap, Dictionary<long, string> serviceOrderMap)
    {
        SetStep("Invoices");
        var step = new MigrationStepResult { Name = "Invoices" };

        var invoices = await ReadAll<PmInvoice>(conn,
            "SELECT id, status, due_at, currency_code, user_id, number, created_at, updated_at FROM invoices");
        var invoiceItems = await ReadAll<PmInvoiceItem>(conn,
            "SELECT id, invoice_id, price, quantity, description, reference_type, reference_id, created_at, updated_at FROM invoice_items");

        var itemsByInvoice = invoiceItems.GroupBy(i => i.InvoiceId).ToDictionary(g => g.Key, g => g.ToList());

        int invoiceSeq = 1;

        foreach (var pm in invoices)
        {
            try
            {
                if (!userMap.TryGetValue(pm.UserId, out var userId))
                {
                    step.Skipped++;
                    continue;
                }

                var items = itemsByInvoice.GetValueOrDefault(pm.Id) ?? new List<PmInvoiceItem>();

                // Try to find the order ID from service references
                var orderId = string.Empty;
                foreach (var item in items)
                {
                    if (item.ReferenceType != null
                        && item.ReferenceType.Contains("Service", StringComparison.OrdinalIgnoreCase)
                        && item.ReferenceId != null
                        && serviceOrderMap.TryGetValue(item.ReferenceId.Value, out var oid)
                        && !string.IsNullOrEmpty(oid))
                    {
                        orderId = oid;
                        break;
                    }
                }

                var lines = items.Select(it => new InvoiceLine
                {
                    Description = it.Description ?? "Service",
                    Quantity = it.Quantity,
                    UnitPrice = it.Price,
                    Total = it.Price * it.Quantity
                }).ToList();

                var subtotal = lines.Sum(l => l.Total);
                var status = MapInvoiceStatus(pm.Status);

                var invoiceNumber = !string.IsNullOrWhiteSpace(pm.Number)
                    ? pm.Number
                    : $"PM-{invoiceSeq++}";

                var invoice = new Invoice
                {
                    UserId = userId,
                    OrderId = orderId,
                    InvoiceNumber = invoiceNumber,
                    Status = status,
                    Lines = lines,
                    Subtotal = subtotal,
                    Tax = 0,
                    Total = subtotal,
                    Currency = pm.CurrencyCode,
                    DueDate = pm.DueAt ?? pm.CreatedAt ?? DateTime.UtcNow,
                    PaidAt = status == InvoiceStatus.Paid ? (pm.UpdatedAt ?? pm.CreatedAt) : null,
                    CreatedAt = pm.CreatedAt ?? DateTime.UtcNow,
                    UpdatedAt = pm.UpdatedAt ?? DateTime.UtcNow
                };

                await _invoiceRepo.CreateAsync(invoice);
                step.Imported++;
            }
            catch (Exception ex)
            {
                step.Failed++;
                step.Warnings.Add($"Invoice {pm.Id}: {ex.Message}");
            }
        }

        step.Success = step.Failed == 0;
        CompleteStep(step);
    }

    private async Task MigratePayments(MySqlConnection conn,
        Dictionary<long, string> userMap, MySqlConnection extensionConn)
    {
        SetStep("Payments");
        var step = new MigrationStepResult { Name = "Payments" };

        var transactions = await ReadAll<PmInvoiceTransaction>(conn,
            "SELECT id, invoice_id, gateway_id, amount, fee, transaction_id, status, created_at, updated_at FROM invoice_transactions");
        var invoices = await ReadAll<PmInvoice>(conn,
            "SELECT id, status, due_at, currency_code, user_id, number, created_at, updated_at FROM invoices");
        var extensions = await ReadAll<PmExtension>(conn,
            "SELECT id, name, type, enabled FROM extensions");

        var invoiceMap = invoices.ToDictionary(i => i.Id);
        var extensionMap = extensions.ToDictionary(e => e.Id, e => e.Name);

        var ogInvoices = (await _invoiceRepo.GetAllAsync()).ToList();
        var ogInvoiceByNumber = new Dictionary<string, Invoice>();
        foreach (var inv in ogInvoices)
        {
            if (!string.IsNullOrEmpty(inv.InvoiceNumber))
                ogInvoiceByNumber.TryAdd(inv.InvoiceNumber, inv);
        }

        foreach (var pm in transactions)
        {
            try
            {
                if (!invoiceMap.TryGetValue(pm.InvoiceId, out var pmInvoice))
                {
                    step.Skipped++;
                    continue;
                }

                if (!userMap.TryGetValue(pmInvoice.UserId, out var userId))
                {
                    step.Skipped++;
                    continue;
                }

                var invoiceNumber = pmInvoice.Number ?? $"PM-{pmInvoice.Id}";
                var ogInvoiceId = ogInvoiceByNumber.TryGetValue(invoiceNumber, out var ogInv) ? ogInv.Id : string.Empty;

                var gateway = pm.GatewayId != null && extensionMap.TryGetValue(pm.GatewayId.Value, out var gw) ? gw : "Unknown";

                var payment = new Payment
                {
                    InvoiceId = ogInvoiceId,
                    UserId = userId,
                    Gateway = gateway,
                    TransactionId = pm.TransactionId,
                    Amount = pm.Amount,
                    Currency = pmInvoice.CurrencyCode,
                    Status = MapPaymentStatus(pm.Status),
                    CreatedAt = pm.CreatedAt ?? DateTime.UtcNow,
                    UpdatedAt = pm.UpdatedAt ?? DateTime.UtcNow
                };

                await _paymentRepo.CreateAsync(payment);
                step.Imported++;
            }
            catch (Exception ex)
            {
                step.Failed++;
                step.Warnings.Add($"Transaction {pm.Id}: {ex.Message}");
            }
        }

        step.Success = step.Failed == 0;
        CompleteStep(step);
    }

    private async Task MigrateTickets(MySqlConnection conn,
        Dictionary<long, string> userMap, Dictionary<long, string> serviceOrderMap)
    {
        SetStep("Tickets");
        var step = new MigrationStepResult { Name = "Tickets" };

        var tickets = await ReadAll<PmTicket>(conn,
            "SELECT id, subject, status, priority, department, user_id, assigned_to, service_id, created_at, updated_at FROM tickets");
        var messages = await ReadAll<PmTicketMessage>(conn,
            "SELECT id, ticket_id, user_id, message, created_at, updated_at FROM ticket_messages");

        var msgsByTicket = messages.GroupBy(m => m.TicketId).ToDictionary(g => g.Key, g => g.OrderBy(m => m.CreatedAt).ToList());

        var pmUsers = await ReadAll<PmUser>(conn,
            "SELECT id, first_name, last_name, email, password, role_id, email_verified_at, created_at, updated_at FROM users");
        var pmUserMap = pmUsers.ToDictionary(u => u.Id);

        foreach (var pm in tickets)
        {
            try
            {
                if (!userMap.TryGetValue(pm.UserId, out var userId))
                {
                    step.Skipped++;
                    continue;
                }

                var orderId = pm.ServiceId != null && serviceOrderMap.TryGetValue(pm.ServiceId.Value, out var oid) ? oid : null;
                var assignedTo = pm.AssignedTo != null && userMap.TryGetValue(pm.AssignedTo.Value, out var aid) ? aid : null;

                var ticketMsgs = msgsByTicket.GetValueOrDefault(pm.Id) ?? new List<PmTicketMessage>();

                var ogMessages = ticketMsgs.Select(m =>
                {
                    var senderName = "Unknown";
                    var isStaff = false;
                    if (pmUserMap.TryGetValue(m.UserId, out var sender))
                    {
                        senderName = $"{sender.FirstName} {sender.LastName}".Trim();
                        if (string.IsNullOrWhiteSpace(senderName)) senderName = sender.Email;
                        isStaff = sender.RoleId != null;
                    }

                    var senderId = userMap.TryGetValue(m.UserId, out var sid) ? sid : string.Empty;

                    return new TicketMessage
                    {
                        SenderId = senderId,
                        SenderName = senderName,
                        IsStaff = isStaff,
                        Body = m.Message,
                        CreatedAt = m.CreatedAt ?? DateTime.UtcNow
                    };
                }).ToList();

                var status = MapTicketStatus(pm.Status);

                var ticket = new Ticket
                {
                    UserId = userId,
                    Subject = pm.Subject,
                    Status = status,
                    Priority = MapTicketPriority(pm.Priority),
                    AssignedTo = assignedTo,
                    OrderId = orderId,
                    Messages = ogMessages,
                    ClosedAt = status == TicketStatus.Closed ? (pm.UpdatedAt ?? DateTime.UtcNow) : null,
                    CreatedAt = pm.CreatedAt ?? DateTime.UtcNow,
                    UpdatedAt = pm.UpdatedAt ?? DateTime.UtcNow
                };

                await _ticketRepo.CreateAsync(ticket);
                step.Imported++;
            }
            catch (Exception ex)
            {
                step.Failed++;
                step.Warnings.Add($"Ticket {pm.Id}: {ex.Message}");
            }
        }

        step.Success = step.Failed == 0;
        CompleteStep(step);
    }

    // -- Helpers --

    private void SetStep(string name)
    {
        lock (_lock) { _progress.CurrentStep = name; }
    }

    private void CompleteStep(MigrationStepResult step)
    {
        lock (_lock)
        {
            _progress.Steps.Add(step);
            _progress.CompletedSteps++;
        }
    }

    private static async Task<int> CountTable(MySqlConnection conn, string table)
    {
        await using var cmd = new MySqlCommand($"SELECT COUNT(*) FROM `{table}`", conn);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private static async Task<List<T>> ReadAll<T>(MySqlConnection conn, string sql) where T : new()
    {
        var list = new List<T>();
        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var props = typeof(T).GetProperties();
        var columnMap = new Dictionary<string, System.Reflection.PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in props)
        {
            columnMap[prop.Name] = prop;
            columnMap[ToSnakeCase(prop.Name)] = prop;
        }

        while (await reader.ReadAsync())
        {
            var obj = new T();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var colName = reader.GetName(i);
                if (!columnMap.TryGetValue(colName, out var prop)) continue;

                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                if (value == null)
                {
                    if (Nullable.GetUnderlyingType(prop.PropertyType) != null || !prop.PropertyType.IsValueType)
                        prop.SetValue(obj, null);
                    continue;
                }

                var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                if (targetType == typeof(bool) && value is long or int or short or sbyte)
                    value = Convert.ToInt64(value) != 0;
                else if (targetType == typeof(decimal))
                    value = Convert.ToDecimal(value);
                else if (targetType == typeof(int))
                    value = Convert.ToInt32(value);
                else if (targetType == typeof(long))
                    value = Convert.ToInt64(value);
                else if (targetType == typeof(DateTime))
                    value = Convert.ToDateTime(value);
                else if (targetType == typeof(string))
                    value = value.ToString();

                prop.SetValue(obj, value);
            }
            list.Add(obj);
        }

        return list;
    }

    private static string ToSnakeCase(string name)
    {
        return string.Concat(name.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c) : char.ToLower(c).ToString()));
    }

    private static BillingCycle MapBillingCycle(string type, int? period, string? unit)
    {
        if (type is "free" or "one-time") return BillingCycle.OneTime;
        return unit?.ToLower() switch
        {
            "year" => BillingCycle.Annually,
            "month" => period switch
            {
                3 => BillingCycle.Quarterly,
                6 => BillingCycle.SemiAnnually,
                12 => BillingCycle.Annually,
                _ => BillingCycle.Monthly
            },
            _ => BillingCycle.Monthly
        };
    }

    private static OrderStatus MapOrderStatus(string status) => status.ToLower() switch
    {
        "active" => OrderStatus.Active,
        "suspended" => OrderStatus.Suspended,
        "cancelled" or "canceled" => OrderStatus.Cancelled,
        "terminated" => OrderStatus.Terminated,
        _ => OrderStatus.Pending
    };

    private static InvoiceStatus MapInvoiceStatus(string status) => status.ToLower() switch
    {
        "paid" => InvoiceStatus.Paid,
        "cancelled" or "canceled" => InvoiceStatus.Cancelled,
        "overdue" => InvoiceStatus.Overdue,
        "refunded" => InvoiceStatus.Refunded,
        _ => InvoiceStatus.Unpaid
    };

    private static PaymentStatus MapPaymentStatus(string? status) => status?.ToLower() switch
    {
        "succeeded" or "completed" or "paid" => PaymentStatus.Completed,
        "failed" => PaymentStatus.Failed,
        "refunded" => PaymentStatus.Refunded,
        _ => PaymentStatus.Pending
    };

    private static TicketStatus MapTicketStatus(string status) => status.ToLower() switch
    {
        "closed" => TicketStatus.Closed,
        "answered" => TicketStatus.Answered,
        "customer-reply" or "customer_reply" => TicketStatus.CustomerReply,
        _ => TicketStatus.Open
    };

    private static TicketPriority MapTicketPriority(string priority) => priority.ToLower() switch
    {
        "low" => TicketPriority.Low,
        "high" => TicketPriority.High,
        "urgent" or "critical" => TicketPriority.Critical,
        _ => TicketPriority.Medium
    };
}
