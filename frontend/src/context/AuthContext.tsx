import { createContext, useContext, useState, type ReactNode } from "react";
import { AuthApi } from "../api/services";

interface AuthUser {
  email: string;
  fullName: string;
  roles: string[];
}

interface AuthContextValue {
  user: AuthUser | null;
  login: (companyCode: string, email: string, password: string) => Promise<void>;
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
    const authUser = { email: data.email, fullName: data.fullName, roles: data.roles };
    localStorage.setItem("user", JSON.stringify(authUser));
    setUser(authUser);
  }

  function logout() {
    localStorage.removeItem("token");
    localStorage.removeItem("user");
    setUser(null);
  }

  return <AuthContext.Provider value={{ user, login, logout }}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
