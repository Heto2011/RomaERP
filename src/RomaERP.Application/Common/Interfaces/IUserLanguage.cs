namespace RomaERP.Application.Common.Interfaces;

/// <summary>The language the current caller's interface is set to (the frontend sends it on every request as
/// Accept-Language). English is the product's base language; Arabic only when explicitly chosen. Used so
/// AI replies and cap messages come back in the language the customer is actually using.</summary>
public interface IUserLanguage
{
    bool PrefersArabic { get; }
}
