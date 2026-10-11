using System.Globalization;
using System.Net;
using System.Text;
using RomaERP.Domain.HR;
using RomaERP.Domain.Tenancy;

namespace RomaERP.Application.HR.Services;

/// <summary>Builds the printable payslip HTML for one employee in one pay run, rendered to PDF via IHtmlToPdfRenderer.
/// Every user-supplied string is HTML-encoded: this HTML is rendered by a real headless browser on the server.</summary>
public static class PayslipHtmlTemplate
{
    public static string Build(PayrollRun run, PayrollRunLine line, CompanySettings settings, bool arabic)
    {
        var culture = CultureInfo.InvariantCulture;
        string Money(decimal amount) => amount.ToString("N2", culture);
        string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
        string L(string ar, string en) => arabic ? ar : en;

        var employee = line.Employee!;
        var currency = Enc(settings.DefaultCurrency);
        var isUk = settings.Country == Country.UnitedKingdom;

        var statutory = line.GosiEmployeeDeductionAmount + line.EgEmployeeInsurance + line.EgIncomeTax + line.UkIncomeTax + line.UkEmployeeNi + line.UkStudentLoan + line.UkPostgraduateLoan + line.UkPensionEmployee;
        var otherDeductions = Math.Max(0, line.TotalDeductions - line.UnpaidLeaveDeductionAmount - statutory);

        var earnings = new StringBuilder();
        void Earn(string label, decimal amount, bool show = true)
        {
            if (show) earnings.Append($"<tr><td>{Enc(label)}</td><td class=\"num\">{Money(amount)}</td></tr>");
        }
        Earn(L("الراتب الأساسي", "Basic salary"), line.BasicSalary);
        Earn(L("البدلات", "Allowances"), line.TotalAllowances, line.TotalAllowances != 0);

        var deductions = new StringBuilder();
        void Deduct(string label, decimal amount, bool show = true)
        {
            if (show) deductions.Append($"<tr><td>{Enc(label)}</td><td class=\"num\">{Money(amount)}</td></tr>");
        }
        Deduct(L("خصم الإجازات بدون راتب", "Unpaid leave"), line.UnpaidLeaveDeductionAmount, line.UnpaidLeaveDeductionAmount != 0);
        Deduct(L("خصومات أخرى", "Other deductions"), otherDeductions, otherDeductions != 0);
        Deduct(L("التأمينات الاجتماعية (حصة الموظف)", "Social insurance (employee)"), line.GosiEmployeeDeductionAmount, line.GosiEmployeeDeductionAmount != 0);
        Deduct(L("التأمينات الاجتماعية (حصة الموظف)", "Social insurance (employee)"), line.EgEmployeeInsurance, line.EgEmployeeInsurance != 0);
        Deduct(L("ضريبة كسب العمل", "Salary income tax"), line.EgIncomeTax, line.EgIncomeTax != 0);
        Deduct(L("ضريبة الدخل (PAYE)", "Income tax (PAYE)"), line.UkIncomeTax, line.UkIncomeTax != 0);
        Deduct(L("التأمين الوطني (حصة الموظف)", "National Insurance (employee)"), line.UkEmployeeNi, line.UkEmployeeNi != 0);
        Deduct(L("قرض الطالب", "Student loan"), line.UkStudentLoan, line.UkStudentLoan != 0);
        Deduct(L("قرض الدراسات العليا", "Postgraduate loan"), line.UkPostgraduateLoan, line.UkPostgraduateLoan != 0);
        Deduct(L("معاش الموظف", "Employee pension"), line.UkPensionEmployee, line.UkPensionEmployee != 0);

        var employer = new StringBuilder();
        void Employer(string label, decimal amount)
        {
            if (amount != 0) employer.Append($"<tr><td>{Enc(label)}</td><td class=\"num\">{Money(amount)}</td></tr>");
        }
        Employer(L("التأمينات الاجتماعية (حصة الشركة)", "Social insurance (employer)"), line.GosiEmployerContributionAmount);
        Employer(L("التأمينات الاجتماعية (حصة الشركة)", "Social insurance (employer)"), line.EgEmployerInsurance);
        Employer(L("التأمين الوطني (حصة الشركة)", "National Insurance (employer)"), line.UkEmployerNi);
        Employer(L("معاش (حصة الشركة)", "Pension (employer)"), line.UkPensionEmployer);
        var employerBlock = employer.Length == 0
            ? string.Empty
            : $"<h3>{L("مساهمات الشركة (لا تُخصم من الراتب)", "Employer contributions (not deducted from pay)")}</h3><table>{employer}</table>";

        var niLine = isUk && !string.IsNullOrWhiteSpace(employee.UkNationalInsuranceNumber)
            ? $"<div>{L("رقم التأمين الوطني", "NI number")}: <b>{Enc(employee.UkNationalInsuranceNumber)}</b> · {L("كود الضريبة", "Tax code")}: <b>{Enc(string.IsNullOrWhiteSpace(employee.UkTaxCode) ? "1257L" : employee.UkTaxCode)}</b></div>"
            : string.Empty;

        var dir = arabic ? "rtl" : "ltr";
        var align = arabic ? "right" : "left";
        return $$"""
            <!doctype html>
            <html dir="{{dir}}" lang="{{(arabic ? "ar" : "en")}}">
            <head>
            <meta charset="utf-8" />
            <style>
                @page { size: A4; margin: 0; }
                * { box-sizing: border-box; }
                body { font-family: "Segoe UI", Tahoma, Arial, sans-serif; color: #1a1a1a; margin: 0; font-size: 13px; }
                .page { padding: 26px 30px; }
                .header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 3px solid #132C39; padding-bottom: 12px; margin-bottom: 16px; }
                .company { font-size: 20px; font-weight: 700; color: #132C39; margin: 0 0 2px; }
                .sub { color: #666; font-size: 12px; }
                h1 { font-size: 20px; margin: 0; color: #132C39; }
                .meta { font-size: 12px; color: #444; line-height: 1.8; }
                .box { background: #f5f6f7; border-radius: 6px; padding: 10px 14px; margin-bottom: 16px; line-height: 1.8; }
                h3 { font-size: 13px; margin: 16px 0 6px; color: #132C39; }
                table { width: 100%; border-collapse: collapse; }
                td { padding: 6px 10px; border-bottom: 1px solid #e5e5e5; text-align: {{align}}; }
                td.num { text-align: {{(arabic ? "left" : "right")}}; width: 140px; font-variant-numeric: tabular-nums; }
                .net { margin-top: 18px; background: #132C39; color: #fff; border-radius: 6px; padding: 12px 16px; display: flex; justify-content: space-between; font-size: 16px; font-weight: 700; }
                .foot { margin-top: 26px; text-align: center; font-size: 10.5px; color: #999; }
            </style>
            </head>
            <body>
              <div class="page">
                <div class="header">
                  <div>
                    <p class="company">{{Enc(arabic ? settings.CompanyNameAr : (string.IsNullOrWhiteSpace(settings.CompanyNameEn) ? settings.CompanyNameAr : settings.CompanyNameEn))}}</p>
                    <div class="sub">{{Enc(arabic ? settings.CompanyNameEn : settings.CompanyNameAr)}}</div>
                  </div>
                  <div>
                    <h1>{{L("كشف راتب", "Payslip")}}</h1>
                    <div class="meta">{{L("تاريخ الدفع", "Pay date")}}: <b>{{run.RunDate:yyyy-MM-dd}}</b><br />{{Enc(run.Description)}}</div>
                  </div>
                </div>
                <div class="box">
                  <div><b>{{Enc(arabic ? employee.FullNameAr : (string.IsNullOrWhiteSpace(employee.FullNameEn) ? employee.FullNameAr : employee.FullNameEn))}}</b></div>
                  <div>{{L("كود الموظف", "Employee code")}}: {{Enc(employee.EmployeeCode)}}</div>
                  {{niLine}}
                </div>
                <h3>{{L("الاستحقاقات", "Earnings")}} ({{currency}})</h3>
                <table>{{earnings}}</table>
                <h3>{{L("الخصومات", "Deductions")}} ({{currency}})</h3>
                <table>{{(deductions.Length == 0 ? $"<tr><td>{L("لا يوجد", "None")}</td><td class=\"num\">0.00</td></tr>" : deductions.ToString())}}</table>
                <div class="net"><span>{{L("صافي الراتب", "Net pay")}}</span><span>{{Money(line.NetSalary)}} {{currency}}</span></div>
                {{employerBlock}}
                <div class="foot">{{L("تم إصدار هذا الكشف عن طريق نظام روما", "Issued by Roma")}}</div>
              </div>
            </body>
            </html>
            """;
    }
}
