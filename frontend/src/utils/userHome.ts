import { ProductScope } from "../api/types";

interface HomeUser {
  roles: string[];
  modules: string[];
  productScope?: ProductScope;
}

/** An Employee-role user with nothing granted beyond the point of sale: they only ever see the cashier screens. */
export function isPureCashier(user: HomeUser | null | undefined): boolean {
  return !!user && user.roles.length === 1 && user.roles[0] === "Employee" && user.modules.every((m) => m === "POS");
}

/** An Employee-role user who was given other modules (say Purchasing): the cashier nav plus those sections. */
export function isEmployeeWithExtraModules(user: HomeUser | null | undefined): boolean {
  return !!user && user.roles.length === 1 && user.roles[0] === "Employee" && user.modules.some((m) => m !== "POS");
}

/** Where an Employee-role user with extra modules lands: the first granted module's main screen. */
export function extraModuleHome(user: HomeUser): string {
  if (user.modules.includes("Purchasing")) return "/purchasing/invoices";
  if (user.modules.includes("Sales")) return "/sales/invoices";
  if (user.modules.includes("Accounting")) return "/accounting/journal-entries";
  if (user.modules.includes("Inventory")) return "/inventory/items";
  if (user.modules.includes("Reports")) return "/accounting/income-statement";
  return "/my-profile";
}
