using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using RomaERP.API.Controllers;
using RomaERP.Application.Common;
using Xunit;

namespace RomaERP.UnitTests;

/// <summary>An ordinary employee must only reach their own HR data — everything that exposes colleagues is HR-only.</summary>
public class EmployeePrivacyTests
{
    private static string? PolicyOf(Type controller, string action) =>
        controller.GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).FirstOrDefault(p => p != null)
        ?? controller.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).FirstOrDefault(p => p != null);

    [Theory]
    [InlineData(typeof(EmployeeRequestsController), nameof(EmployeeRequestsController.GetCalendar))]
    [InlineData(typeof(EmployeeRequestsController), nameof(EmployeeRequestsController.GetAll))]
    [InlineData(typeof(EmployeeRequestsController), nameof(EmployeeRequestsController.GetPending))]
    [InlineData(typeof(EmployeesController), nameof(EmployeesController.GetAll))]
    [InlineData(typeof(EmployeeContractsController), nameof(EmployeeContractsController.GetAll))]
    [InlineData(typeof(EmployeeContractsController), nameof(EmployeeContractsController.DownloadFile))]
    public void ColleagueData_RequiresTheHrPolicy(Type controller, string action)
    {
        Assert.Equal(ModulePermissions.HRPolicy, PolicyOf(controller, action));
    }
}
