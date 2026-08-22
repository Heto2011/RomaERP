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

export enum ChatRole {
  User = 1,
  Assistant = 2,
}

export enum ExpenseCaptureStatus {
  AwaitingDetails = 1,
  AwaitingPaymentMethod = 2,
  AwaitingReconciliation = 3,
  Posted = 4,
  Rejected = 5,
}

export enum ExpensePaymentMethod {
  Unknown = 0,
  Cash = 1,
  Card = 2,
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
