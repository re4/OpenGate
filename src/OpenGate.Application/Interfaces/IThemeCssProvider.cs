namespace OpenGate.Application.Interfaces;

public interface IThemeCssProvider
{
    Task<string> GetCssAsync();
    void InvalidateCache();
}
