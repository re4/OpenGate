using OpenGate.Application.DTOs;

namespace OpenGate.Web.Services;

public class CartService
{
    private readonly List<CartItemDto> _items = new();

    public IReadOnlyList<CartItemDto> Items => _items.AsReadOnly();

    public int Count => _items.Count;

    public decimal Total => _items.Sum(i => i.UnitPrice * i.Quantity);

    public event Action? OnChange;

    public void AddItem(CartItemDto item)
    {
        var existing = _items.FirstOrDefault(i =>
            i.ProductId == item.ProductId &&
            OptionsMatch(i.SelectedOptions, item.SelectedOptions));

        if (existing != null)
        {
            existing.Quantity += item.Quantity;
        }
        else
        {
            _items.Add(item);
        }
        OnChange?.Invoke();
    }

    public void RemoveItem(int index)
    {
        if (index >= 0 && index < _items.Count)
        {
            _items.RemoveAt(index);
            OnChange?.Invoke();
        }
    }

    public void UpdateQuantity(int index, int quantity)
    {
        if (index >= 0 && index < _items.Count && quantity > 0)
        {
            _items[index].Quantity = quantity;
            OnChange?.Invoke();
        }
    }

    public void Clear()
    {
        _items.Clear();
        OnChange?.Invoke();
    }

    private static bool OptionsMatch(Dictionary<string, string>? a, Dictionary<string, string>? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        return a.All(kvp => b.TryGetValue(kvp.Key, out var val) && val == kvp.Value);
    }
}
