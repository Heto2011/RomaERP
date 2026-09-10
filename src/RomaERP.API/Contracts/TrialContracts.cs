using RomaERP.Domain.Tenancy;

namespace RomaERP.API.Contracts;

public record TrialSignupRequest(
    string CompanyNameAr,
    string CompanyNameEn,
    Country Country,
    string AdminFullName,
    string AdminEmail,
    string AdminPassword);

/// <summary>Carries a ready-to-use login token so the new tenant lands straight in the app —
/// no separate login step right after signing up.</summary>
public record TrialSignupResponse(
    string Token,
    string CompanyCode,
    string Email,
    string FullName,
    List<string> Roles,
    DateTime? ExpiresAtUtc);
