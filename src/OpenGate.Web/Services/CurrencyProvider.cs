using OpenGate.Domain.Interfaces;

namespace OpenGate.Web.Services;

public class CurrencyProvider(IServiceScopeFactory scopeFactory)
{
    private CurrencyInfo? _cached;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<CurrencyInfo> GetAsync()
    {
        if (_cached != null) return _cached;

        await _lock.WaitAsync();
        try
        {
            if (_cached != null) return _cached;

            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISettingRepository>();

            var currency = (await repo.GetByKeyAsync("Currency"))?.Value ?? "USD";
            var rateStr = (await repo.GetByKeyAsync("TaxRate"))?.Value ?? "0";
            var label = (await repo.GetByKeyAsync("TaxLabel"))?.Value ?? "Tax";
            var inclusiveStr = (await repo.GetByKeyAsync("TaxInclusive"))?.Value ?? "false";

            decimal.TryParse(rateStr, out var rate);
            var code = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();

            _cached = new CurrencyInfo
            {
                CurrencyCode = code,
                Symbol = CurrencySymbols.GetValueOrDefault(code, code),
                TaxRate = rate,
                TaxLabel = string.IsNullOrWhiteSpace(label) ? "Tax" : label.Trim(),
                TaxInclusive = string.Equals(inclusiveStr, "true", StringComparison.OrdinalIgnoreCase),
            };

            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void InvalidateCache() => _cached = null;

    private static readonly Dictionary<string, string> CurrencySymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = "$",
        ["EUR"] = "€",
        ["GBP"] = "£",
        ["CAD"] = "C$",
        ["AUD"] = "A$",
        ["JPY"] = "¥",
        ["CHF"] = "Fr",
        ["CNY"] = "¥",
        ["INR"] = "₹",
        ["BRL"] = "R$",
        ["MXN"] = "Mex$",
        ["KRW"] = "₩",
        ["SEK"] = "kr",
        ["NOK"] = "kr",
        ["DKK"] = "kr",
        ["PLN"] = "zł",
        ["TRY"] = "₺",
        ["ZAR"] = "R",
        ["SGD"] = "S$",
        ["HKD"] = "HK$",
        ["NZD"] = "NZ$",
        ["CZK"] = "Kč",
        ["ILS"] = "₪",
        ["THB"] = "฿",
        ["AED"] = "د.إ",
        ["SAR"] = "﷼",
        ["RUB"] = "₽",
        ["PHP"] = "₱",
        ["TWD"] = "NT$",
        ["IDR"] = "Rp",
        ["MYR"] = "RM",
        ["CLP"] = "CL$",
        ["ARS"] = "AR$",
        ["COP"] = "COL$",
        ["EGP"] = "E£",
        ["NGN"] = "₦",
        ["BGN"] = "лв",
        ["HUF"] = "Ft",
        ["RON"] = "lei",
        ["HRK"] = "kn",
        ["UAH"] = "₴",
    };
}

public class CurrencyInfo
{
    public string CurrencyCode { get; set; } = "USD";
    public string Symbol { get; set; } = "$";
    public decimal TaxRate { get; set; }
    public string TaxLabel { get; set; } = "Tax";
    public bool TaxInclusive { get; set; }

    public bool TaxEnabled => TaxRate > 0;

    public string Format(decimal amount) => $"{Symbol}{amount:N2}";

    public decimal CalculateTax(decimal subtotal)
    {
        if (TaxRate <= 0) return 0;
        if (TaxInclusive)
            return subtotal - (subtotal / (1 + TaxRate / 100));
        return subtotal * TaxRate / 100;
    }

    public decimal CalculateTotal(decimal subtotal)
    {
        if (TaxRate <= 0) return subtotal;
        if (TaxInclusive) return subtotal;
        return subtotal + (subtotal * TaxRate / 100);
    }

    public string TaxDisplay => TaxRate > 0 ? $"{TaxLabel} ({TaxRate:G}%)" : TaxLabel;
}
