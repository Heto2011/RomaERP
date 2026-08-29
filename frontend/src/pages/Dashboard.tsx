import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  AccountsApi,
  AiAssistantApi,
  EmployeesApi,
  JournalEntriesApi,
  PurchasingApi,
  SalesApi,
} from "../api/services";
import { useLanguage } from "../i18n/LanguageContext";
import {
  IconChat,
  IconList,
  IconUsers,
  IconTruck,
  IconBook,
  IconBox,
  IconCart,
  IconUser,
} from "../components/icons";

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
  const [overdueCustomers, setOverdueCustomers] = useState(0);
  const [overdueVendors, setOverdueVendors] = useState(0);
  const [pendingApprovals, setPendingApprovals] = useState(0);

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
    SalesApi.getAging().then((r) => {
      setOverdueCustomers(r.data.filter((c) => c.days31To60 + c.days61To90 + c.over90Days > 0).length);
    });
    PurchasingApi.getAging().then((r) => {
      setOverdueVendors(r.data.filter((v) => v.days31To60 + v.days61To90 + v.over90Days > 0).length);
    });
    AiAssistantApi.getPendingApproval().then((r) => setPendingApprovals(r.data.length));
  }, []);

  const quickLinks = [
    { to: "/assistant/chat", label: t.nav.assistantChat, icon: <IconChat /> },
    { to: "/accounting/journal-entries", label: t.nav.journalEntries, icon: <IconBook /> },
    { to: "/sales/customers", label: t.nav.customers, icon: <IconUsers /> },
    { to: "/sales/invoices", label: t.nav.salesInvoices, icon: <IconList /> },
    { to: "/purchasing/vendors", label: t.nav.vendors, icon: <IconTruck /> },
    { to: "/inventory/items", label: t.nav.items, icon: <IconBox /> },
    { to: "/restaurant/pos", label: t.nav.restaurantPos, icon: <IconCart /> },
    { to: "/my-profile", label: t.nav.myProfile, icon: <IconUser /> },
  ];

  const alerts = [
    overdueCustomers > 0 && { to: "/sales/aging", count: overdueCustomers, label: t.dashboard.overdueCustomers },
    overdueVendors > 0 && { to: "/purchasing/aging", count: overdueVendors, label: t.dashboard.overdueVendors },
    pendingApprovals > 0 && { to: "/assistant/approvals", count: pendingApprovals, label: t.dashboard.pendingApprovals },
  ].filter(Boolean) as { to: string; count: number; label: string }[];

  const salesVsPurchasesMax = Math.max(totalSales, totalPurchases, 1);
  const arVsApMax = Math.max(arOutstanding, apOutstanding, 1);

  return (
    <div>
      <div className="page-header">
        <h1>{t.dashboard.title}</h1>
      </div>

      <div className="dash-app-grid">
        {quickLinks.map((item) => (
          <Link key={item.to} to={item.to} className="dash-app-tile">
            <span className="dash-app-tile-icon">{item.icon}</span>
            <span>{item.label}</span>
          </Link>
        ))}
      </div>

      <h3 style={{ marginTop: 24 }}>{t.dashboard.needsAttention}</h3>
      <div className="dash-alerts">
        {alerts.length === 0 && <div className="dash-alert dash-alert-clear">✓ {t.dashboard.allClear}</div>}
        {alerts.map((a) => (
          <Link key={a.to} to={a.to} className="dash-alert">
            <span className="dash-alert-count">{a.count}</span>
            <span>{a.label}</span>
          </Link>
        ))}
      </div>

      <div className="stat-grid" style={{ marginTop: 24 }}>
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

      <h3 style={{ marginTop: 24 }}>{t.dashboard.financialSnapshot}</h3>
      <div className="dash-snapshot-grid">
        <div className="card">
          <div className="dash-snapshot-title">{t.dashboard.salesVsPurchases}</div>
          <div className="dash-bar-row">
            <span className="dash-bar-label">{t.dashboard.totalSales}</span>
            <div className="dash-bar-track">
              <div className="dash-bar-fill dash-bar-primary" style={{ width: `${(totalSales / salesVsPurchasesMax) * 100}%` }} />
            </div>
            <span className="dash-bar-value">{totalSales.toLocaleString()}</span>
          </div>
          <div className="dash-bar-row">
            <span className="dash-bar-label">{t.dashboard.totalPurchases}</span>
            <div className="dash-bar-track">
              <div className="dash-bar-fill dash-bar-accent" style={{ width: `${(totalPurchases / salesVsPurchasesMax) * 100}%` }} />
            </div>
            <span className="dash-bar-value">{totalPurchases.toLocaleString()}</span>
          </div>
        </div>
        <div className="card">
          <div className="dash-snapshot-title">{t.dashboard.arVsAp}</div>
          <div className="dash-bar-row">
            <span className="dash-bar-label">{t.dashboard.arOutstanding}</span>
            <div className="dash-bar-track">
              <div className="dash-bar-fill dash-bar-primary" style={{ width: `${(arOutstanding / arVsApMax) * 100}%` }} />
            </div>
            <span className="dash-bar-value">{arOutstanding.toLocaleString()}</span>
          </div>
          <div className="dash-bar-row">
            <span className="dash-bar-label">{t.dashboard.apOutstanding}</span>
            <div className="dash-bar-track">
              <div className="dash-bar-fill dash-bar-accent" style={{ width: `${(apOutstanding / arVsApMax) * 100}%` }} />
            </div>
            <span className="dash-bar-value">{apOutstanding.toLocaleString()}</span>
          </div>
        </div>
      </div>
    </div>
  );
}
