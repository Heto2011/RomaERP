import { useEffect, useState } from "react";
import axios from "axios";
import { EmployeesApi, PayrollApi } from "../api/services";
import { PayrollRunStatus, type Employee, type MyPayslip } from "../api/types";
import { useLanguage } from "../i18n/LanguageContext";
import { bilingualName } from "../i18n/bilingual";

export default function MyProfile() {
  const { t, lang } = useLanguage();
  const [profile, setProfile] = useState<Employee | null>(null);
  const [payslips, setPayslips] = useState<MyPayslip[]>([]);
  const [notLinked, setNotLinked] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        const res = await EmployeesApi.getMyProfile();
        setProfile(res.data);
        const payslipsRes = await PayrollApi.getMyPayslips();
        setPayslips(payslipsRes.data);
      } catch (err) {
        if (axios.isAxiosError(err) && err.response?.status === 404) setNotLinked(true);
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const statusLabel: Record<PayrollRunStatus, string> = {
    [PayrollRunStatus.Draft]: t.accounting.draft,
    [PayrollRunStatus.Approved]: t.hr.approved,
    [PayrollRunStatus.Posted]: t.accounting.posted,
  };

  return (
    <div>
      <div className="page-header">
        <h1>{t.myProfile.title}</h1>
      </div>

      {loading && <div className="text-muted">{t.common.loading}</div>}

      {!loading && notLinked && (
        <div className="card">
          <p className="text-muted" style={{ marginTop: 0 }}>{t.myProfile.notLinked}</p>
        </div>
      )}

      {!loading && profile && (
        <>
          <div className="card">
            <div className="form-grid">
              <div className="form-field">
                <label>{t.hr.employeeCode}</label>
                <div>{profile.employeeCode}</div>
              </div>
              <div className="form-field">
                <label>{t.common.nameAr}</label>
                <div>{bilingualName(profile.fullNameAr, profile.fullNameEn, lang)}</div>
              </div>
              <div className="form-field">
                <label>{t.hr.department}</label>
                <div>{profile.departmentName}</div>
              </div>
              <div className="form-field">
                <label>{t.hr.position}</label>
                <div>{profile.positionName}</div>
              </div>
              <div className="form-field">
                <label>{t.hr.hireDate}</label>
                <div>{new Date(profile.hireDate).toLocaleDateString()}</div>
              </div>
              <div className="form-field">
                <label>{t.hr.basicSalary}</label>
                <div>{profile.basicSalary.toLocaleString()}</div>
              </div>
              <div className="form-field">
                <label>{t.common.email}</label>
                <div>{profile.email ?? "-"}</div>
              </div>
              <div className="form-field">
                <label>{t.common.phone}</label>
                <div>{profile.phone ?? "-"}</div>
              </div>
            </div>
          </div>

          <div className="card">
            <h3 style={{ marginTop: 0 }}>{t.myProfile.payslipsTitle}</h3>
            <table>
              <thead>
                <tr>
                  <th>{t.hr.payDate}</th>
                  <th>{t.hr.basicSalary}</th>
                  <th>{t.hr.allowances}</th>
                  <th>{t.hr.deductions}</th>
                  <th>{t.hr.netSalary}</th>
                  <th>{t.common.status}</th>
                </tr>
              </thead>
              <tbody>
                {payslips.length === 0 && (
                  <tr>
                    <td colSpan={6} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                      {t.common.noData}
                    </td>
                  </tr>
                )}
                {payslips.map((p, i) => (
                  <tr key={i}>
                    <td>{new Date(p.runDate).toLocaleDateString()}</td>
                    <td>{p.basicSalary.toLocaleString()}</td>
                    <td>{p.totalAllowances.toLocaleString()}</td>
                    <td>{p.totalDeductions.toLocaleString()}</td>
                    <td>{p.netSalary.toLocaleString()}</td>
                    <td>
                      <span className={`badge ${p.status === PayrollRunStatus.Posted ? "badge-posted" : "badge-draft"}`}>
                        {statusLabel[p.status]}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}
