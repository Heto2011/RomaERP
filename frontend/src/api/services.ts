import { apiClient } from "./client";
import type {
  Account,
  BalanceSheet,
  BankStatementImportResult,
  BankStatementLine,
  ChatTurnResponse,
  CostCenterLookup,
  Department,
  Employee,
  ExpenseCapture,
  FiscalPeriod,
  FiscalYearDetail,
  IncomeStatement,
  Item,
  ItemCategory,
  JournalEntry,
  PayrollRun,
  Position,
  SalaryComponent,
  StockMovement,
  TrialBalanceLine,
  Warehouse,
} from "./types";

export const AuthApi = {
  login: (companyCode: string, email: string, password: string) =>
    apiClient.post<{ token: string; email: string; fullName: string; roles: string[] }>(
      "/auth/login",
      { email, password },
      { headers: { "X-Company-Code": companyCode } }
    ),
};

export const AccountsApi = {
  getTree: () => apiClient.get<Account[]>("/accounts/tree"),
  getAll: () => apiClient.get<Account[]>("/accounts"),
  create: (data: Partial<Account>) => apiClient.post<Account>("/accounts", data),
  update: (id: string, data: Partial<Account>) => apiClient.put<Account>(`/accounts/${id}`, data),
  remove: (id: string) => apiClient.delete(`/accounts/${id}`),
};

export const JournalEntriesApi = {
  getAll: () => apiClient.get<JournalEntry[]>("/journalentries"),
  getById: (id: string) => apiClient.get<JournalEntry>(`/journalentries/${id}`),
  create: (data: unknown) => apiClient.post<JournalEntry>("/journalentries", data),
  post: (id: string) => apiClient.post<JournalEntry>(`/journalentries/${id}/post`),
  reverse: (id: string) => apiClient.post<JournalEntry>(`/journalentries/${id}/reverse`),
  trialBalance: (asOfDate?: string) =>
    apiClient.get<TrialBalanceLine[]>("/journalentries/trial-balance", {
      params: asOfDate ? { asOfDate } : undefined,
    }),
};

export const LookupsApi = {
  fiscalPeriods: () => apiClient.get<FiscalPeriod[]>("/lookups/fiscal-periods"),
  costCenters: () => apiClient.get<CostCenterLookup[]>("/lookups/cost-centers"),
};

export const DepartmentsApi = {
  getAll: () => apiClient.get<Department[]>("/departments"),
  create: (data: Partial<Department>) => apiClient.post<Department>("/departments", data),
  update: (id: string, data: Partial<Department>) => apiClient.put<Department>(`/departments/${id}`, data),
  remove: (id: string) => apiClient.delete(`/departments/${id}`),
};

export const PositionsApi = {
  getAll: () => apiClient.get<Position[]>("/positions"),
  create: (data: Partial<Position>) => apiClient.post<Position>("/positions", data),
  update: (id: string, data: Partial<Position>) => apiClient.put<Position>(`/positions/${id}`, data),
  remove: (id: string) => apiClient.delete(`/positions/${id}`),
};

export const EmployeesApi = {
  getAll: () => apiClient.get<Employee[]>("/employees"),
  getById: (id: string) => apiClient.get<Employee>(`/employees/${id}`),
  create: (data: Partial<Employee>) => apiClient.post<Employee>("/employees", data),
  update: (id: string, data: Partial<Employee>) => apiClient.put<Employee>(`/employees/${id}`, data),
  remove: (id: string) => apiClient.delete(`/employees/${id}`),
};

export const SalaryComponentsApi = {
  getAll: () => apiClient.get<SalaryComponent[]>("/salarycomponents"),
  create: (data: Partial<SalaryComponent>) => apiClient.post<SalaryComponent>("/salarycomponents", data),
  assign: (employeeId: string, salaryComponentId: string, value: number) =>
    apiClient.post(`/salarycomponents/employees/${employeeId}/assign`, { salaryComponentId, value }),
  remove: (employeeId: string, salaryComponentId: string) =>
    apiClient.delete(`/salarycomponents/employees/${employeeId}/${salaryComponentId}`),
};

export const PayrollApi = {
  getAll: () => apiClient.get<PayrollRun[]>("/payroll"),
  getById: (id: string) => apiClient.get<PayrollRun>(`/payroll/${id}`),
  create: (data: unknown) => apiClient.post<PayrollRun>("/payroll", data),
  approve: (id: string) => apiClient.post<PayrollRun>(`/payroll/${id}/approve`),
  post: (id: string) => apiClient.post<PayrollRun>(`/payroll/${id}/post`),
};

export const AiAssistantApi = {
  sendMessage: (captureId: string | null, message: string) =>
    apiClient.post<ChatTurnResponse>("/aiassistant/messages", { captureId, message }),
  getPendingReconciliation: () => apiClient.get<ExpenseCapture[]>("/aiassistant/pending-reconciliation"),
  getPendingApproval: () => apiClient.get<ExpenseCapture[]>("/aiassistant/pending-approval"),
  approve: (captureId: string) => apiClient.post<ExpenseCapture>(`/aiassistant/captures/${captureId}/approve`),
  reject: (captureId: string) => apiClient.post<ExpenseCapture>(`/aiassistant/captures/${captureId}/reject`),
  uploadProof: (captureId: string, file: File) => {
    const formData = new FormData();
    formData.append("file", file);
    return apiClient.post<ExpenseCapture>(`/aiassistant/captures/${captureId}/proof`, formData, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },
};

export const BankReconciliationApi = {
  import: (file: File, bankAccountId: string) => {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("bankAccountId", bankAccountId);
    return apiClient.post<BankStatementImportResult>("/bankreconciliation/import", formData, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },
  getUnmatchedLines: () => apiClient.get<BankStatementLine[]>("/bankreconciliation/unmatched-lines"),
  autoMatch: () => apiClient.post<number>("/bankreconciliation/auto-match"),
  matchManual: (expenseCaptureId: string, bankStatementLineId: string) =>
    apiClient.post<ExpenseCapture>("/bankreconciliation/match", { expenseCaptureId, bankStatementLineId }),
};

export const OpeningBalanceApi = {
  getForFiscalYear: (fiscalYearId: string) =>
    apiClient.get<JournalEntry | null>(`/openingbalance/fiscal-years/${fiscalYearId}`),
  create: (data: unknown) => apiClient.post<JournalEntry>("/openingbalance", data),
};

export const FinancialReportsApi = {
  incomeStatement: (fromDate: string, toDate: string) =>
    apiClient.get<IncomeStatement>("/financialreports/income-statement", { params: { fromDate, toDate } }),
  balanceSheet: (asOfDate: string) =>
    apiClient.get<BalanceSheet>("/financialreports/balance-sheet", { params: { asOfDate } }),
};

export const FiscalPeriodsAdminApi = {
  getAllYears: () => apiClient.get<FiscalYearDetail[]>("/fiscalperiods/years"),
  closePeriod: (id: string) => apiClient.post<FiscalPeriod>(`/fiscalperiods/${id}/close`),
  reopenPeriod: (id: string) => apiClient.post<FiscalPeriod>(`/fiscalperiods/${id}/reopen`),
  closeYear: (id: string) => apiClient.post<FiscalYearDetail>(`/fiscalperiods/years/${id}/close`),
};

export const ItemCategoriesApi = {
  getAll: () => apiClient.get<ItemCategory[]>("/itemcategories"),
  create: (data: Partial<ItemCategory>) => apiClient.post<ItemCategory>("/itemcategories", data),
  remove: (id: string) => apiClient.delete(`/itemcategories/${id}`),
};

export const WarehousesApi = {
  getAll: () => apiClient.get<Warehouse[]>("/warehouses"),
  create: (data: Partial<Warehouse>) => apiClient.post<Warehouse>("/warehouses", data),
  remove: (id: string) => apiClient.delete(`/warehouses/${id}`),
};

export const ItemsApi = {
  getAll: () => apiClient.get<Item[]>("/items"),
  create: (data: Partial<Item>) => apiClient.post<Item>("/items", data),
  remove: (id: string) => apiClient.delete(`/items/${id}`),
};

export const InventoryApi = {
  getMovements: () => apiClient.get<StockMovement[]>("/inventory/movements"),
  receive: (data: unknown) => apiClient.post<StockMovement>("/inventory/receive", data),
  issue: (data: unknown) => apiClient.post<StockMovement>("/inventory/issue", data),
};
