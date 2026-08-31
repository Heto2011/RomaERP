export enum AccountType {
  Asset = 1,
  Liability = 2,
  Equity = 3,
  Revenue = 4,
  Expense = 5,
}

export enum AccountNature {
  Debit = 1,
  Credit = 2,
}

export enum JournalEntryStatus {
  Draft = 1,
  Posted = 2,
  Reversed = 3,
}

export interface Account {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  accountType: AccountType;
  nature: AccountNature;
  parentAccountId: string | null;
  isControlAccount: boolean;
  isActive: boolean;
  level: number;
  children: Account[];
}

export interface JournalEntryLine {
  id: string;
  lineNumber: number;
  accountId: string;
  accountCode: string;
  accountName: string;
  costCenterId: string | null;
  debit: number;
  credit: number;
  description: string | null;
}

export interface JournalEntry {
  id: string;
  entryNumber: string;
  entryDate: string;
  fiscalPeriodId: string;
  description: string | null;
  reference: string | null;
  status: JournalEntryStatus;
  totalDebit: number;
  totalCredit: number;
  lines: JournalEntryLine[];
}

export enum DepreciationMethod {
  StraightLine = 1,
  DecliningBalance = 2,
}

export enum FixedAssetStatus {
  Active = 1,
  Disposed = 2,
}

export enum DepreciationRunStatus {
  Draft = 1,
  Posted = 2,
}

export interface FixedAsset {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  assetAccountId: string;
  assetAccountCode: string;
  assetAccountName: string;
  accumulatedDepreciationAccountId: string;
  accumulatedDepreciationAccountCode: string;
  accumulatedDepreciationAccountName: string;
  acquisitionCost: number;
  acquisitionDate: string;
  usefulLifeYears: number;
  salvageValue: number;
  depreciationMethod: DepreciationMethod;
  decliningBalanceRate: number | null;
  accumulatedDepreciation: number;
  bookValue: number;
  status: FixedAssetStatus;
}

export interface CreateFixedAssetInput {
  code: string;
  nameAr: string;
  nameEn: string;
  assetAccountId: string;
  accumulatedDepreciationAccountId: string;
  acquisitionCost: number;
  acquisitionDate: string;
  usefulLifeYears: number;
  salvageValue: number;
  depreciationMethod: DepreciationMethod;
  decliningBalanceRate: number | null;
}

export interface DepreciationRunLine {
  fixedAssetId: string;
  assetCode: string;
  assetName: string;
  amount: number;
}

export interface DepreciationRun {
  id: string;
  fiscalPeriodId: string;
  runDate: string;
  status: DepreciationRunStatus;
  description: string | null;
  journalEntryId: string | null;
  lines: DepreciationRunLine[];
  totalAmount: number;
}

export interface CreateDepreciationRunInput {
  fiscalPeriodId: string;
  runDate: string;
  description: string | null;
}

export interface ReportLine {
  accountCode: string;
  accountName: string;
  amount: number;
}

export interface IncomeStatement {
  fromDate: string;
  toDate: string;
  revenueLines: ReportLine[];
  totalRevenue: number;
  expenseLines: ReportLine[];
  totalExpense: number;
  netIncome: number;
}

export interface BalanceSheet {
  asOfDate: string;
  assetLines: ReportLine[];
  totalAssets: number;
  liabilityLines: ReportLine[];
  totalLiabilities: number;
  equityLines: ReportLine[];
  currentYearNetIncome: number;
  totalEquity: number;
  totalLiabilitiesAndEquity: number;
  isBalanced: boolean;
}

export interface CostCenterAnalysisLine {
  costCenterId: string | null;
  costCenterCode: string;
  costCenterName: string;
  revenueBreakdown: ReportLine[];
  totalRevenue: number;
  expenseBreakdown: ReportLine[];
  totalExpense: number;
  netAmount: number;
}

export interface CostCenterAnalysis {
  fromDate: string;
  toDate: string;
  costCenters: CostCenterAnalysisLine[];
}

export interface VatSummary {
  fromDate: string;
  toDate: string;
  outputVat: number;
  inputVat: number;
  netVatPayable: number;
}

export interface CashFlowLine {
  categoryCode: string;
  categoryName: string;
  amount: number;
}

export interface CashFlowStatement {
  fromDate: string;
  toDate: string;
  beginningCash: number;
  cashInLines: CashFlowLine[];
  totalCashIn: number;
  cashOutLines: CashFlowLine[];
  totalCashOut: number;
  netCashChange: number;
  endingCash: number;
}

export interface CashFlowProjectedWeek {
  weekStart: string;
  projectedNetChange: number;
  projectedEndingBalance: number;
  isBelowZero: boolean;
}

export interface CashFlowIntelligence {
  asOfDate: string;
  currentCashBalance: number;
  historicalWeeksUsed: number;
  isLowConfidence: boolean;
  averageWeeklyNetChange: number;
  projectedWeeks: CashFlowProjectedWeek[];
  firstWeekBelowZero: string | null;
}

export interface EmployeeSalesLine {
  employeeId: string;
  employeeName: string;
  salesTotal: number;
  orderCount: number;
}

export interface LaborReport {
  fromDate: string;
  toDate: string;
  totalPayroll: number;
  totalSalesRevenue: number;
  laborCostPercent: number | null;
  salesByEmployee: EmployeeSalesLine[];
}

export interface ItemProfitabilityLine {
  itemId: string;
  itemCode: string;
  itemName: string;
  quantitySold: number;
  revenue: number;
  cost: number;
  grossProfit: number;
  marginPercent: number;
}

export interface ItemProfitabilityReport {
  fromDate: string;
  toDate: string;
  items: ItemProfitabilityLine[];
}

export interface CustomerProfitabilityLine {
  customerId: string;
  customerName: string;
  revenue: number;
  cost: number;
  grossProfit: number;
  marginPercent: number;
}

export interface CustomerProfitabilityReport {
  fromDate: string;
  toDate: string;
  customers: CustomerProfitabilityLine[];
}

export interface SalesChannelProfitabilityLine {
  channel: RestaurantOrderType;
  revenue: number;
  cost: number;
  grossProfit: number;
  marginPercent: number;
}

export interface SalesChannelProfitabilityReport {
  fromDate: string;
  toDate: string;
  channels: SalesChannelProfitabilityLine[];
}

export enum ManualProfitDimension {
  Branch = 1,
  Channel = 2,
}

export interface ManualProfitEntry {
  id: string;
  dimension: ManualProfitDimension;
  name: string;
  periodMonth: string;
  revenue: number;
  cost: number;
  grossProfit: number;
  marginPercent: number;
}

export interface CreateManualProfitEntry {
  dimension: ManualProfitDimension;
  name: string;
  periodMonth: string;
  revenue: number;
  cost: number;
}

export interface UpdateManualProfitEntry {
  name: string;
  periodMonth: string;
  revenue: number;
  cost: number;
}

export interface StockValuationLine {
  itemId: string;
  itemCode: string;
  itemName: string;
  categoryName: string;
  quantityOnHand: number;
  averageCost: number;
  value: number;
}

export interface StockValuationReport {
  asOfDate: string;
  items: StockValuationLine[];
  totalValue: number;
}

export interface InventoryMovementLine {
  itemId: string;
  itemCode: string;
  itemName: string;
  quantityOnHand: number;
  reorderLevel: number;
  stockValue: number;
  quantityIssuedInPeriod: number;
  cogsInPeriod: number;
  daysOfStockRemaining: number | null;
  turnoverRate: number;
  isAtRiskOfStockout: boolean;
  isDeadStock: boolean;
  isExcessStock: boolean;
}

export interface InventoryMovementReport {
  fromDate: string;
  toDate: string;
  items: InventoryMovementLine[];
}

export interface PurchasePriceVarianceLine {
  itemId: string;
  itemCode: string;
  itemName: string;
  previousReceiptDate: string;
  previousUnitCost: number;
  latestReceiptDate: string;
  latestUnitCost: number;
  changeAmount: number;
  changePercent: number;
}

export interface PurchasePriceVarianceReport {
  fromDate: string;
  toDate: string;
  items: PurchasePriceVarianceLine[];
}

export interface RecipeCostLine {
  itemId: string;
  itemCode: string;
  itemName: string;
  hasRecipe: boolean;
  recipeCost: number;
  menuPrice: number;
  grossProfit: number;
  marginPercent: number;
}

export interface RecipeCostReport {
  items: RecipeCostLine[];
}

export interface WasteByItemLine {
  itemId: string;
  itemCode: string;
  itemName: string;
  totalQuantity: number;
  totalCost: number;
  entryCount: number;
}

export interface WasteByReasonLine {
  reason: WasteReason;
  totalCost: number;
  percentOfTotal: number;
}

export interface WasteTrendPoint {
  weekStart: string;
  totalCost: number;
}

export interface WasteAnalysisReport {
  fromDate: string;
  toDate: string;
  totalWasteCost: number;
  totalWasteQuantity: number;
  cogsInPeriod: number;
  wasteCostPercentOfCogs: number | null;
  topWastedItems: WasteByItemLine[];
  byReason: WasteByReasonLine[];
  weeklyTrend: WasteTrendPoint[];
}

export interface PhysicalStockCountEntry {
  id: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  countDate: string;
  systemQuantity: number;
  countedQuantity: number;
  variance: number;
  unitCost: number;
  varianceValue: number;
  notes: string | null;
}

export interface CreatePhysicalStockCount {
  itemId: string;
  countDate: string;
  countedQuantity: number;
  notes: string | null;
}

export enum WasteReason {
  Waste = 1,
  Expired = 2,
  Damaged = 3,
  ProductionWaste = 4,
  OverPortion = 5,
  Unknown = 6,
}

export interface WasteEntryRecord {
  id: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  wasteDate: string;
  quantity: number;
  unitCost: number;
  totalCost: number;
  reason: WasteReason;
  notes: string | null;
}

export interface CreateWasteEntry {
  itemId: string;
  warehouseId: string;
  fiscalPeriodId: string;
  wasteDate: string;
  quantity: number;
  reason: WasteReason;
  notes: string | null;
}

export interface HiddenProfitLine {
  reasonCode: string;
  amount: number;
}

export interface HistoricalMonth {
  monthLabel: string;
  revenue: number;
  expense: number;
  netIncome: number;
}

export interface ForecastMonth {
  monthLabel: string;
  expectedRevenue: number;
  worstRevenue: number;
  bestRevenue: number;
  expectedExpense: number;
  expectedProfit: number;
  worstProfit: number;
  bestProfit: number;
}

export interface ForecastReport {
  historicalMonthsUsed: number;
  isLowConfidence: boolean;
  historicalMonths: HistoricalMonth[];
  forecastMonths: ForecastMonth[];
}

export interface HiddenProfitReport {
  fromDate: string;
  toDate: string;
  lines: HiddenProfitLine[];
  totalImpact: number;
}

export enum ChatRole {
  User = 1,
  Assistant = 2,
}

export enum ExpenseCaptureStatus {
  AwaitingDetails = 1,
  AwaitingFundingSource = 2,
  AwaitingCustodyEmployee = 3,
  AwaitingPaymentMethod = 4,
  AwaitingReconciliation = 5,
  PendingApproval = 6,
  Posted = 7,
  Rejected = 8,
}

export enum ExpensePaymentMethod {
  Unknown = 0,
  Cash = 1,
  Card = 2,
}

export enum ExpenseFundingSource {
  Unknown = 0,
  CompanyAccount = 1,
  EmployeeCustody = 2,
}

export interface ChatMessage {
  role: ChatRole;
  content: string;
  createdAtUtc: string;
}

export interface ExpenseCapture {
  id: string;
  rawText: string;
  amount: number | null;
  currency: string;
  description: string | null;
  entryDate: string;
  suggestedAccountId: string | null;
  suggestedAccountCode: string | null;
  suggestedAccountName: string | null;
  fundingSource: ExpenseFundingSource;
  custodyEmployeeId: string | null;
  custodyEmployeeName: string | null;
  paymentMethod: ExpensePaymentMethod;
  status: ExpenseCaptureStatus;
  proofFileName: string | null;
  journalEntryId: string | null;
  submittedByUserId: string;
}

export interface ChatTurnResponse {
  captureId: string;
  status: ExpenseCaptureStatus;
  assistantReply: string;
  history: ChatMessage[];
  capture: ExpenseCapture | null;
}

export interface BankStatementLine {
  id: string;
  transactionDate: string;
  description: string;
  amount: number;
  isMatched: boolean;
}

export interface BankStatementImportResult {
  id: string;
  fileName: string;
  bankAccountName: string;
  lineCount: number;
  matchedCount: number;
}

export interface DeliverySettlementImportResult {
  id: string;
  fileName: string;
  platformName: string;
  periodFrom: string;
  periodTo: string;
  totalAmount: number;
  lineCount: number;
}

export interface DeliveryReconciliationReport {
  fromDate: string;
  toDate: string;
  expectedRevenue: number;
  receivedAmount: number;
  variance: number;
}

export interface FiscalYearDetail {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  isClosed: boolean;
  periods: FiscalPeriod[];
}

export interface TrialBalanceLine {
  accountCode: string;
  accountName: string;
  accountType: AccountType;
  totalDebit: number;
  totalCredit: number;
  balance: number;
}

export interface CostCenterLookup {
  id: string;
  code: string;
  nameAr: string;
}

export interface CompanySettingsLookup {
  vatRate: number;
  defaultCurrency: string;
}

export interface FiscalPeriod {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  isClosed: boolean;
}

export interface Department {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  parentDepartmentId: string | null;
  managerId: string | null;
  isActive: boolean;
}

export interface Position {
  id: string;
  code: string;
  titleAr: string;
  titleEn: string;
  departmentId: string;
  isActive: boolean;
}

export enum Gender {
  Male = 1,
  Female = 2,
}

export enum MaritalStatus {
  Single = 1,
  Married = 2,
  Divorced = 3,
  Widowed = 4,
}

export enum EmploymentStatus {
  Active = 1,
  OnLeave = 2,
  Terminated = 3,
}

export interface Employee {
  id: string;
  employeeCode: string;
  fullNameAr: string;
  fullNameEn: string;
  nationalId: string | null;
  birthDate: string | null;
  gender: Gender;
  maritalStatus: MaritalStatus;
  hireDate: string;
  terminationDate: string | null;
  employmentStatus: EmploymentStatus;
  departmentId: string;
  departmentName: string;
  positionId: string;
  positionName: string;
  basicSalary: number;
  email: string | null;
  phone: string | null;
  address: string | null;
  bankAccountNumber: string | null;
  iban: string | null;
  applicationUserId: string | null;
}

export enum SalaryComponentType {
  Allowance = 1,
  Deduction = 2,
}

export enum CalculationType {
  FixedAmount = 1,
  PercentageOfBasic = 2,
}

export interface SalaryComponent {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  componentType: SalaryComponentType;
  calculationType: CalculationType;
  defaultValue: number;
  isTaxable: boolean;
  linkedAccountId: string | null;
  isActive: boolean;
}

export enum PayrollRunStatus {
  Draft = 1,
  Approved = 2,
  Posted = 3,
}

export interface PayrollRunLine {
  employeeId: string;
  employeeName: string;
  basicSalary: number;
  totalAllowances: number;
  totalDeductions: number;
  netSalary: number;
}

export interface PayrollRun {
  id: string;
  fiscalPeriodId: string;
  runDate: string;
  status: PayrollRunStatus;
  description: string | null;
  journalEntryId: string | null;
  lines: PayrollRunLine[];
  totalNet: number;
}

export interface MyPayslip {
  runDate: string;
  status: PayrollRunStatus;
  description: string | null;
  basicSalary: number;
  totalAllowances: number;
  totalDeductions: number;
  netSalary: number;
}

export interface ItemCategory {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
}

export interface Warehouse {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
}

export interface Item {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  unitOfMeasure: string;
  itemCategoryId: string;
  itemCategoryName: string;
  reorderLevel: number;
  quantityOnHand: number;
  averageCost: number;
  isActive: boolean;
  isMenuItem: boolean;
  menuPrice: number;
}

export enum StockMovementType {
  Receipt = 1,
  Issue = 2,
}

export interface StockMovement {
  id: string;
  movementNumber: string;
  movementDate: string;
  movementType: StockMovementType;
  itemId: string;
  itemCode: string;
  itemName: string;
  warehouseId: string;
  warehouseName: string;
  quantity: number;
  unitCost: number;
  totalCost: number;
  reference: string | null;
  description: string | null;
  journalEntryId: string | null;
}

export enum PaymentTerm {
  Cash = 1,
  Card = 2,
  Credit = 3,
  Installment = 4,
}

export interface Customer {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  phone: string | null;
  email: string | null;
  taxRegistrationNumber: string | null;
  isActive: boolean;
  arBalance: number;
}

export interface CreateCustomerInput {
  code: string;
  nameAr: string;
  nameEn: string;
  phone?: string | null;
  email?: string | null;
  taxRegistrationNumber?: string | null;
}

export interface SalesInvoiceLineInput {
  description: string;
  quantity: number;
  unitPrice: number;
  itemId?: string | null;
}

export interface SalesInvoiceLine {
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  itemId: string | null;
  itemCode: string | null;
  itemName: string | null;
}

export interface SalesPayment {
  id: string;
  paymentDate: string;
  amount: number;
  method: PaymentTerm;
  reference: string | null;
  journalEntryId: string | null;
}

export interface CreateSalesInvoiceInput {
  customerId: string;
  invoiceDate: string;
  fiscalPeriodId: string;
  paymentTerm: PaymentTerm;
  notes?: string | null;
  warehouseId?: string | null;
  lines: SalesInvoiceLineInput[];
  /// Required only when paymentTerm is Installment.
  numberOfInstallments?: number | null;
  firstInstallmentDueDate?: string | null;
}

export interface SalesInstallmentLine {
  installmentNumber: number;
  dueDate: string;
  amount: number;
  isPaid: boolean;
}

export interface RecordSalesPaymentInput {
  amount: number;
  method: PaymentTerm;
  paymentDate: string;
  reference?: string | null;
}

export interface SalesInvoice {
  id: string;
  invoiceNumber: string;
  invoiceDate: string;
  customerId: string;
  customerName: string;
  subTotal: number;
  vatRate: number;
  vatAmount: number;
  totalAmount: number;
  paymentTerm: PaymentTerm;
  paidAmount: number;
  outstandingAmount: number;
  journalEntryId: string | null;
  notes: string | null;
  warehouseId: string | null;
  warehouseName: string | null;
  lines: SalesInvoiceLine[];
  payments: SalesPayment[];
  installmentLines: SalesInstallmentLine[];
  eInvoiceStatus: EInvoiceStatus;
  eInvoiceExternalUuid: string | null;
  eInvoiceSubmittedAtUtc: string | null;
  eInvoiceErrorMessage: string | null;
}

export enum SalesNoteType {
  Credit = 1,
  Debit = 2,
}

export interface SalesNoteLineInput {
  description: string;
  quantity: number;
  unitPrice: number;
}

export interface SalesNoteLine {
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface CreateSalesNoteInput {
  originalInvoiceId: string;
  noteType: SalesNoteType;
  noteDate: string;
  fiscalPeriodId: string;
  reason: string;
  notes?: string | null;
  lines: SalesNoteLineInput[];
}

export interface SalesNote {
  id: string;
  noteNumber: string;
  noteType: SalesNoteType;
  noteDate: string;
  originalInvoiceId: string;
  originalInvoiceNumber: string;
  customerId: string;
  customerName: string;
  fiscalPeriodId: string;
  reason: string;
  subTotal: number;
  vatRate: number;
  vatAmount: number;
  totalAmount: number;
  journalEntryId: string | null;
  notes: string | null;
  lines: SalesNoteLine[];
  eInvoiceStatus: EInvoiceStatus;
  eInvoiceExternalUuid: string | null;
  eInvoiceSubmittedAtUtc: string | null;
  eInvoiceErrorMessage: string | null;
}

export interface EInvoiceNoteStatusDto {
  salesNoteId: string;
  status: EInvoiceStatus;
  externalUuid: string | null;
  submittedAtUtc: string | null;
  errorMessage: string | null;
}

export interface Vendor {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  phone: string | null;
  email: string | null;
  taxRegistrationNumber: string | null;
  isActive: boolean;
  apBalance: number;
}

export interface CreateVendorInput {
  code: string;
  nameAr: string;
  nameEn: string;
  phone?: string | null;
  email?: string | null;
  taxRegistrationNumber?: string | null;
}

export interface PurchaseInvoiceLineInput {
  description: string;
  accountId: string;
  itemId?: string | null;
  quantity: number;
  unitPrice: number;
}

export interface PurchaseInvoiceLine {
  description: string;
  accountId: string;
  accountCode: string;
  accountName: string;
  itemId: string | null;
  itemCode: string | null;
  itemName: string | null;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface PurchasePayment {
  id: string;
  paymentDate: string;
  amount: number;
  method: PaymentTerm;
  reference: string | null;
  journalEntryId: string | null;
}

export interface CreatePurchaseInvoiceInput {
  vendorId: string;
  invoiceDate: string;
  fiscalPeriodId: string;
  paymentTerm: PaymentTerm;
  notes?: string | null;
  lines: PurchaseInvoiceLineInput[];
}

export interface RecordPurchasePaymentInput {
  amount: number;
  method: PaymentTerm;
  paymentDate: string;
  reference?: string | null;
}

export interface PurchaseInvoice {
  id: string;
  invoiceNumber: string;
  invoiceDate: string;
  vendorId: string;
  vendorName: string;
  subTotal: number;
  vatRate: number;
  vatAmount: number;
  totalAmount: number;
  paymentTerm: PaymentTerm;
  paidAmount: number;
  outstandingAmount: number;
  journalEntryId: string | null;
  notes: string | null;
  lines: PurchaseInvoiceLine[];
  payments: PurchasePayment[];
}

export interface CustomerAging {
  customerId: string;
  customerCode: string;
  customerName: string;
  totalOutstanding: number;
  current: number;
  days31To60: number;
  days61To90: number;
  over90Days: number;
}

export interface VendorAging {
  vendorId: string;
  vendorCode: string;
  vendorName: string;
  totalOutstanding: number;
  current: number;
  days31To60: number;
  days61To90: number;
  over90Days: number;
}

export const AppRoles = ["Admin", "Accountant", "HR", "Employee"] as const;
export type AppRole = (typeof AppRoles)[number];

export interface AppUser {
  id: string;
  email: string;
  fullName: string;
  isActive: boolean;
  roles: string[];
  employeeId: string | null;
  employeeName: string | null;
}

export interface CreateUserInput {
  email: string;
  password: string;
  fullName: string;
  roles: string[];
}

export enum EInvoicingProvider {
  None = 0,
  Eta = 1,
  Zatca = 2,
}

export enum EInvoicingEnvironment {
  Sandbox = 1,
  Production = 2,
}

export enum EInvoiceStatus {
  NotSubmitted = 1,
  Submitted = 2,
  Accepted = 3,
  Rejected = 4,
}

export interface EInvoicingSettings {
  provider: EInvoicingProvider;
  environment: EInvoicingEnvironment;
  hasClientCredentials: boolean;
  hasCertificate: boolean;
}

export interface UpdateEInvoicingSettingsInput {
  provider: EInvoicingProvider;
  environment: EInvoicingEnvironment;
  clientId?: string | null;
  clientSecret?: string | null;
  certificate?: string | null;
  privateKey?: string | null;
}

export interface EInvoiceStatusDto {
  salesInvoiceId: string;
  status: EInvoiceStatus;
  externalUuid: string | null;
  submittedAtUtc: string | null;
  errorMessage: string | null;
}

export enum ZatcaOnboardingStage {
  NotStarted = 1,
  CsrGenerated = 2,
  ComplianceCsidObtained = 3,
  ComplianceChecksPassed = 4,
  ProductionCsidObtained = 5,
}

export interface SaveZatcaOnboardingDetailsInput {
  organizationIdentifier: string;
  solutionName: string;
  model: string;
  deviceSerialNumber: string;
  organizationUnitName: string;
  address: string;
  businessCategory: string;
  invoiceType: string;
}

export interface ZatcaOnboardingStatus {
  stage: ZatcaOnboardingStage;
  hasCsr: boolean;
  complianceRequestId: string | null;
  hasCertificate: boolean;
  hasSecret: boolean;
  lastComplianceCheckStatus: string | null;
}

export enum RestaurantTableStatus {
  Available = 1,
  Occupied = 2,
  Reserved = 3,
}

export interface RestaurantTable {
  id: string;
  number: string;
  sectionName: string | null;
  capacity: number;
  status: RestaurantTableStatus;
}

export interface CreateRestaurantTableInput {
  number: string;
  sectionName?: string | null;
  capacity: number;
}

export interface MenuItem {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  menuPrice: number;
  itemCategoryId: string;
  categoryName: string;
  hasRecipe: boolean;
}

export interface RecipeLine {
  rawMaterialItemId: string;
  rawMaterialCode: string;
  rawMaterialName: string;
  quantityPerUnit: number;
}

export interface SetRecipeLineInput {
  rawMaterialItemId: string;
  quantityPerUnit: number;
}

export interface SetMenuItemInput {
  isMenuItem: boolean;
  menuPrice: number;
  recipeLines: SetRecipeLineInput[];
}

export enum RestaurantOrderType {
  DineIn = 1,
  Takeaway = 2,
  Delivery = 3,
}

export enum RestaurantOrderStatus {
  Open = 1,
  Billed = 2,
  Cancelled = 3,
}

export interface CreateRestaurantOrderInput {
  orderType: RestaurantOrderType;
  tableId?: string | null;
  customerName?: string | null;
  customerPhone?: string | null;
  deliveryAddress?: string | null;
  waiterEmployeeId?: string | null;
  warehouseId: string;
  notes?: string | null;
}

export interface AddOrderLineInput {
  itemId: string;
  quantity: number;
  notes?: string | null;
}

export interface RestaurantOrderLine {
  id: string;
  lineNumber: number;
  itemId: string;
  itemName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  notes: string | null;
}

export interface RestaurantOrder {
  id: string;
  orderNumber: string;
  orderType: RestaurantOrderType;
  orderDate: string;
  tableId: string | null;
  tableNumber: string | null;
  customerName: string | null;
  customerPhone: string | null;
  deliveryAddress: string | null;
  waiterEmployeeId: string | null;
  waiterName: string | null;
  warehouseId: string;
  status: RestaurantOrderStatus;
  notes: string | null;
  salesInvoiceId: string | null;
  salesInvoiceNumber: string | null;
  subTotal: number;
  vatRate: number;
  vatAmount: number;
  totalAmount: number;
  lines: RestaurantOrderLine[];
}

export interface BillOrderInput {
  paymentTerm: PaymentTerm;
  fiscalPeriodId: string;
  cashierShiftId?: string | null;
}

export interface CashierShift {
  id: string;
  employeeId: string;
  employeeName: string;
  openedAtUtc: string;
  openingFloat: number;
  closedAtUtc: string | null;
  closingCountedCash: number | null;
  expectedCash: number | null;
  cashVariance: number | null;
  status: number;
}

export interface OpenCashierShiftInput {
  employeeId: string;
  openingFloat: number;
}

export interface CloseCashierShiftInput {
  closingCountedCash: number;
}

export enum AlertSeverity {
  Info = 1,
  Warning = 2,
  Critical = 3,
}

export interface Alert {
  category: string;
  severity: AlertSeverity;
  title: string;
  detail: string;
}

export interface AlertsReport {
  generatedAt: string;
  alerts: Alert[];
}

export enum Country {
  Egypt = 1,
  SaudiArabia = 2,
  UAE = 3,
  Bahrain = 4,
  Oman = 5,
  Qatar = 6,
  Kuwait = 7,
}

export interface ProvisionTenantRequest {
  companyCode: string;
  companyNameAr: string;
  companyNameEn: string;
  country: Country;
  adminEmail: string;
  adminPassword: string;
  taxRegistrationNumber?: string | null;
  isDemo?: boolean;
  demoExpiryDays?: number | null;
  seedDemoData?: boolean;
}

export interface Tenant {
  id: string;
  companyCode: string;
  companyNameAr: string;
  companyNameEn: string;
  country: Country;
  isActive: boolean;
  isDemo: boolean;
  expiresAtUtc: string | null;
  createdAtUtc: string;
}
