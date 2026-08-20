import { useEffect, useState } from "react";
import { AccountsApi, EmployeesApi, JournalEntriesApi } from "../api/services";

export default function Dashboard() {
  const [accountsCount, setAccountsCount] = useState(0);
  const [employeesCount, setEmployeesCount] = useState(0);
  const [totalDebit, setTotalDebit] = useState(0);
  const [totalCredit, setTotalCredit] = useState(0);

  useEffect(() => {
    AccountsApi.getAll().then((r) => setAccountsCount(r.data.length));
    EmployeesApi.getAll().then((r) => setEmployeesCount(r.data.length));
    JournalEntriesApi.trialBalance().then((r) => {
      setTotalDebit(r.data.reduce((sum, l) => sum + l.totalDebit, 0));
      setTotalCredit(r.data.reduce((sum, l) => sum + l.totalCredit, 0));
    });
  }, []);

  return (
    <div>
      <div className="page-header">
        <h1>لوحة التحكم</h1>
      </div>
      <div className="stat-grid">
        <div className="stat-card">
          <div className="label">عدد الحسابات</div>
          <div className="value">{accountsCount}</div>
        </div>
        <div className="stat-card">
          <div className="label">عدد الموظفين</div>
          <div className="value">{employeesCount}</div>
        </div>
        <div className="stat-card">
          <div className="label">إجمالي المدين (القيود المرحلة)</div>
          <div className="value">{totalDebit.toLocaleString()}</div>
        </div>
        <div className="stat-card">
          <div className="label">إجمالي الدائن (القيود المرحلة)</div>
          <div className="value">{totalCredit.toLocaleString()}</div>
        </div>
      </div>
    </div>
  );
}
