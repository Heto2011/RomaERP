import { createContext, useContext, useState, type ReactNode } from "react";
import { AuthApi } from "../api/services";
import { BusinessType, ProductScope } from "../api/types";

interface AuthUser {
  email: string;
  fullName: string;
  roles: string[];
  modules: string[];
  productScope: ProductScope;
  businessTypes?: BusinessType;
}

interface AuthContextValue {
  user: AuthUser | null;
  login: (companyCode: string, email: string, password: string) => Promise<void>;
  loginWithToken: (companyCode: string, token: string, email: string, fullName: string, roles: string[], modules?: string[], productScope?: ProductScope, businessTypes?: BusinessType) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => {
    const stored = localStorage.getItem("user");
    return stored ? JSON.parse(stored) : null;
  });

  async function login(companyCode: string, email: string, password: string) {
    const { data } = await AuthApi.login(companyCode, email, password);
    localStorage.setItem("companyCode", companyCode);
    localStorage.setItem("token", data.token);
    const authUser = { email: data.email, fullName: data.fullName, roles: data.roles, modules: data.modules, productScope: data.productScope, businessTypes: data.businessTypes };
    localStorage.setItem("user", JSON.stringify(authUser));
    setUser(authUser);
  }

  /// Used when the backend already hands back a ready-to-use token (e.g. right after self-service
  /// trial signup), so the caller doesn't have to immediately turn around and call login() again.
  function loginWithToken(companyCode: string, token: string, email: string, fullName: string, roles: string[], modules: string[] = [], productScope: ProductScope = ProductScope.Full, businessTypes: BusinessType = BusinessType.None) {
    localStorage.setItem("companyCode", companyCode);
    localStorage.setItem("token", token);
    const authUser = { email, fullName, roles, modules, productScope, businessTypes };
    localStorage.setItem("user", JSON.stringify(authUser));
    setUser(authUser);
  }

  function logout() {
    localStorage.removeItem("token");
    localStorage.removeItem("user");
    setUser(null);
  }

  return <AuthContext.Provider value={{ user, login, loginWithToken, logout }}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
