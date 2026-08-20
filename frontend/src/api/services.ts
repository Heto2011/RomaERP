import { apiClient } from "./client";
import type {
  Account,
  Department,
  Employee,
  FiscalPeriod,
  JournalEntry,
  PayrollRun,
  Position,
  SalaryComponent,
  TrialBalanceLine,
} from "./types";

export const AuthApi = {
  login: (email: string, password: string) =>
    apiClient.post<{ token: string; email: string; fullName: string; roles: string[] }>(
      "/auth/login",
      { email, password }
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
  costCenters: () => apiClient.get<{ id: string; code: string; nameAr: string }[]>("/lookups/cost-centers"),
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
