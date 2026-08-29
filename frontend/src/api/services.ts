import { apiClient } from "./client";
import type {
  Account,
  AppUser,
  BalanceSheet,
  CostCenterAnalysis,
  VatSummary,
  CashFlowStatement,
  ItemProfitabilityReport,
  BankStatementImportResult,
  BankStatementLine,
  ChatTurnResponse,
  CompanySettingsLookup,
  CostCenterLookup,
  CreateCustomerInput,
  CreatePurchaseInvoiceInput,
  CreateDepreciationRunInput,
  CreateFixedAssetInput,
  CreateSalesInvoiceInput,
  CreateSalesNoteInput,
  CreateUserInput,
  CreateVendorInput,
  Customer,
  CustomerAging,
  Department,
  DepreciationRun,
  EInvoiceNoteStatusDto,
  EInvoiceStatusDto,
  EInvoicingSettings,
  Employee,
  FixedAsset,
  ExpenseCapture,
  FiscalPeriod,
  FiscalYearDetail,
  IncomeStatement,
  Item,
  ItemCategory,
  JournalEntry,
  MyPayslip,
  PayrollRun,
  Position,
  PurchaseInvoice,
  RecordPurchasePaymentInput,
  RecordSalesPaymentInput,
  SalaryComponent,
  SalesInvoice,
  SalesNote,
  StockMovement,
  SaveZatcaOnboardingDetailsInput,
  TrialBalanceLine,
  UpdateEInvoicingSettingsInput,
  Vendor,
  VendorAging,
  Warehouse,
  ZatcaOnboardingStatus,
  RestaurantTable,
  CreateRestaurantTableInput,
  RestaurantTableStatus,
  MenuItem,
  RecipeLine,
  SetMenuItemInput,
  RestaurantOrder,
  CreateRestaurantOrderInput,
  AddOrderLineInput,
  BillOrderInput,
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
  companySettings: () => apiClient.get<CompanySettingsLookup>("/lookups/company-settings"),
};

export const FixedAssetsApi = {
  getAll: () => apiClient.get<FixedAsset[]>("/fixedassets"),
  getById: (id: string) => apiClient.get<FixedAsset>(`/fixedassets/${id}`),
  create: (data: CreateFixedAssetInput) => apiClient.post<FixedAsset>("/fixedassets", data),
};

export const DepreciationApi = {
  getAll: () => apiClient.get<DepreciationRun[]>("/depreciation"),
  getById: (id: string) => apiClient.get<DepreciationRun>(`/depreciation/${id}`),
  create: (data: CreateDepreciationRunInput) => apiClient.post<DepreciationRun>("/depreciation", data),
  post: (id: string) => apiClient.post<DepreciationRun>(`/depreciation/${id}/post`),
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
  getMyProfile: () => apiClient.get<Employee>("/employees/me"),
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
  getMyPayslips: () => apiClient.get<MyPayslip[]>("/payroll/me"),
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
  startFromReceipt: (file: File) => {
    const formData = new FormData();
    formData.append("file", file);
    return apiClient.post<ChatTurnResponse>("/aiassistant/captures/from-receipt", formData, {
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
  costCenterAnalysis: (fromDate: string, toDate: string) =>
    apiClient.get<CostCenterAnalysis>("/financialreports/cost-center-analysis", { params: { fromDate, toDate } }),
  vatSummary: (fromDate: string, toDate: string) =>
    apiClient.get<VatSummary>("/financialreports/vat-summary", { params: { fromDate, toDate } }),
  cashFlow: (fromDate: string, toDate: string) =>
    apiClient.get<CashFlowStatement>("/financialreports/cash-flow", { params: { fromDate, toDate } }),
  itemProfitability: (fromDate: string, toDate: string) =>
    apiClient.get<ItemProfitabilityReport>("/financialreports/item-profitability", { params: { fromDate, toDate } }),
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

export const SalesApi = {
  getCustomers: () => apiClient.get<Customer[]>("/sales/customers"),
  createCustomer: (data: CreateCustomerInput) => apiClient.post<Customer>("/sales/customers", data),
  getInvoices: () => apiClient.get<SalesInvoice[]>("/sales/invoices"),
  getInvoice: (id: string) => apiClient.get<SalesInvoice>(`/sales/invoices/${id}`),
  createInvoice: (data: CreateSalesInvoiceInput) => apiClient.post<SalesInvoice>("/sales/invoices", data),
  recordPayment: (id: string, data: RecordSalesPaymentInput) => apiClient.post<SalesInvoice>(`/sales/invoices/${id}/payments`, data),
  getAging: () => apiClient.get<CustomerAging[]>("/sales/aging"),
  submitEInvoice: (id: string) => apiClient.post<EInvoiceStatusDto>(`/sales/invoices/${id}/submit-einvoice`),
  downloadInvoicePdf: (id: string) => apiClient.get(`/sales/invoices/${id}/pdf`, { responseType: "blob" }),
  getNotes: () => apiClient.get<SalesNote[]>("/sales/notes"),
  getNote: (id: string) => apiClient.get<SalesNote>(`/sales/notes/${id}`),
  createNote: (data: CreateSalesNoteInput) => apiClient.post<SalesNote>("/sales/notes", data),
  downloadNotePdf: (id: string) => apiClient.get(`/sales/notes/${id}/pdf`, { responseType: "blob" }),
  submitNoteEInvoice: (id: string) => apiClient.post<EInvoiceNoteStatusDto>(`/sales/notes/${id}/submit-einvoice`),
};

export const EInvoicingApi = {
  getSettings: () => apiClient.get<EInvoicingSettings>("/einvoicing/settings"),
  updateSettings: (data: UpdateEInvoicingSettingsInput) => apiClient.put<EInvoicingSettings>("/einvoicing/settings", data),
};

export const ZatcaOnboardingApi = {
  getStatus: () => apiClient.get<ZatcaOnboardingStatus>("/einvoicing/zatca/onboarding"),
  generateCsr: (data: SaveZatcaOnboardingDetailsInput) => apiClient.post<ZatcaOnboardingStatus>("/einvoicing/zatca/onboarding/csr", data),
  requestComplianceCsid: (otp: string) => apiClient.post<ZatcaOnboardingStatus>("/einvoicing/zatca/onboarding/compliance-csid", { otp }),
  runComplianceChecks: () => apiClient.post<ZatcaOnboardingStatus>("/einvoicing/zatca/onboarding/compliance-checks"),
  requestProductionCsid: () => apiClient.post<ZatcaOnboardingStatus>("/einvoicing/zatca/onboarding/production-csid"),
};

export const PurchasingApi = {
  getVendors: () => apiClient.get<Vendor[]>("/purchasing/vendors"),
  createVendor: (data: CreateVendorInput) => apiClient.post<Vendor>("/purchasing/vendors", data),
  getInvoices: () => apiClient.get<PurchaseInvoice[]>("/purchasing/invoices"),
  getInvoice: (id: string) => apiClient.get<PurchaseInvoice>(`/purchasing/invoices/${id}`),
  createInvoice: (data: CreatePurchaseInvoiceInput) => apiClient.post<PurchaseInvoice>("/purchasing/invoices", data),
  recordPayment: (id: string, data: RecordPurchasePaymentInput) => apiClient.post<PurchaseInvoice>(`/purchasing/invoices/${id}/payments`, data),
  getAging: () => apiClient.get<VendorAging[]>("/purchasing/aging"),
  downloadInvoicePdf: (id: string) => apiClient.get(`/purchasing/invoices/${id}/pdf`, { responseType: "blob" }),
};

export const RestaurantApi = {
  getTables: () => apiClient.get<RestaurantTable[]>("/restaurant/tables"),
  createTable: (data: CreateRestaurantTableInput) => apiClient.post<RestaurantTable>("/restaurant/tables", data),
  setTableStatus: (id: string, status: RestaurantTableStatus) =>
    apiClient.put<RestaurantTable>(`/restaurant/tables/${id}/status`, { status }),

  getMenu: () => apiClient.get<MenuItem[]>("/restaurant/menu"),
  getRecipe: (itemId: string) => apiClient.get<RecipeLine[]>(`/restaurant/menu/${itemId}/recipe`),
  setMenuItem: (itemId: string, data: SetMenuItemInput) => apiClient.put(`/restaurant/menu/${itemId}`, data),

  getOrders: (includeClosed = false) => apiClient.get<RestaurantOrder[]>("/restaurant/orders", { params: { includeClosed } }),
  getOrder: (id: string) => apiClient.get<RestaurantOrder>(`/restaurant/orders/${id}`),
  createOrder: (data: CreateRestaurantOrderInput) => apiClient.post<RestaurantOrder>("/restaurant/orders", data),
  addLine: (orderId: string, data: AddOrderLineInput) => apiClient.post<RestaurantOrder>(`/restaurant/orders/${orderId}/lines`, data),
  updateLineQuantity: (orderId: string, lineId: string, quantity: number) =>
    apiClient.put<RestaurantOrder>(`/restaurant/orders/${orderId}/lines/${lineId}`, { quantity }),
  removeLine: (orderId: string, lineId: string) => apiClient.delete<RestaurantOrder>(`/restaurant/orders/${orderId}/lines/${lineId}`),
  cancelOrder: (orderId: string) => apiClient.post<RestaurantOrder>(`/restaurant/orders/${orderId}/cancel`),
  billOrder: (orderId: string, data: BillOrderInput) => apiClient.post<RestaurantOrder>(`/restaurant/orders/${orderId}/bill`, data),
};

export const UsersApi = {
  getAll: () => apiClient.get<AppUser[]>("/users"),
  create: (data: CreateUserInput) => apiClient.post<AppUser>("/users", data),
  updateRoles: (id: string, roles: string[]) => apiClient.put<AppUser>(`/users/${id}/roles`, { roles }),
  deactivate: (id: string) => apiClient.post<AppUser>(`/users/${id}/deactivate`),
  activate: (id: string) => apiClient.post<AppUser>(`/users/${id}/activate`),
  linkEmployee: (id: string, employeeId: string | null) => apiClient.put<AppUser>(`/users/${id}/employee-link`, { employeeId }),
};
