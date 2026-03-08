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
            var code = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();

            _cached = new CurrencyInfo
            {
                CurrencyCode = code,
                Symbol = CurrencySymbols.GetValueOrDefault(code, code),
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

    public string Format(decimal amount) => $"{Symbol}{amount:N2}";
}
