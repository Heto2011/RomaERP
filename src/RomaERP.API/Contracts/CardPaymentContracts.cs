namespace RomaERP.API.Contracts;

public record CardPlanOptionDto(string PlanCode, bool Monthly, bool Annual);
public record CardPaymentOptionsDto(bool Enabled, List<CardPlanOptionDto> Plans);
public record StartCheckoutRequest(string PlanCode, bool Annual);
public record CardCheckoutDto(string Url);
