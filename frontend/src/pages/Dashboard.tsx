import { useEffect, useState } from "react";
import { AccountsApi, EmployeesApi, JournalEntriesApi, PurchasingApi, SalesApi } from "../api/services";
import { useLanguage } from "../i18n/LanguageContext";

export default function Dashboard() {
  const { t } = useLanguage();
  const [accountsCount, setAccountsCount] = useState(0);
  const [employeesCount, setEmployeesCount] = useState(0);
  const [totalDebit, setTotalDebit] = useState(0);
  const [totalCredit, setTotalCredit] = useState(0);
  const [totalSales, setTotalSales] = useState(0);
  const [totalPurchases, setTotalPurchases] = useState(0);
  const [arOutstanding, setArOutstanding] = useState(0);
  const [apOutstanding, setApOutstanding] = useState(0);

  useEffect(() => {
    AccountsApi.getAll().then((r) => setAccountsCount(r.data.length));
    EmployeesApi.getAll().then((r) => setEmployeesCount(r.data.length));
    JournalEntriesApi.trialBalance().then((r) => {
      setTotalDebit(r.data.reduce((sum, l) => sum + l.totalDebit, 0));
      setTotalCredit(r.data.reduce((sum, l) => sum + l.totalCredit, 0));
    });
    SalesApi.getInvoices().then((r) => {
      setTotalSales(r.data.reduce((sum, i) => sum + i.totalAmount, 0));
      setArOutstanding(r.data.reduce((sum, i) => sum + i.outstandingAmount, 0));
    });
    PurchasingApi.getInvoices().then((r) => {
      setTotalPurchases(r.data.reduce((sum, i) => sum + i.totalAmount, 0));
      setApOutstanding(r.data.reduce((sum, i) => sum + i.outstandingAmount, 0));
    });
  }, []);

  return (
    <div>
      <div className="page-header">
        <h1>{t.dashboard.title}</h1>
      </div>
      <div className="stat-grid">
        <div className="stat-card">
          <div className="label">{t.dashboard.accountsCount}</div>
          <div className="value">{accountsCount}</div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.employeesCount}</div>
          <div className="value">{employeesCount}</div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.totalDebit}</div>
          <div className="value">{totalDebit.toLocaleString()}</div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.totalCredit}</div>
          <div className="value">{totalCredit.toLocaleString()}</div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.totalSales}</div>
          <div className="value">{totalSales.toLocaleString()}</div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.totalPurchases}</div>
          <div className="value">{totalPurchases.toLocaleString()}</div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.arOutstanding}</div>
          <div className="value">{arOutstanding.toLocaleString()}</div>
        </div>
        <div className="stat-card">
          <div className="label">{t.dashboard.apOutstanding}</div>
          <div className="value">{apOutstanding.toLocaleString()}</div>
        </div>
      </div>
    </div>
  );
}
