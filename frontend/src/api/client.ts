import axios from "axios";

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL,
});

/// Plain instance with no auth/company-code/401-redirect interceptors — for the not-tenant-scoped
/// /system endpoints, which authenticate with a per-call system key instead of a user session.
export const systemApiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL,
});

apiClient.interceptors.request.use((config) => {
  // English is the base language; the server uses this so AI replies and messages match the UI.
  config.headers["Accept-Language"] = localStorage.getItem("lang") === "ar" ? "ar" : "en";
  const token = localStorage.getItem("token");
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  const companyCode = localStorage.getItem("companyCode");
  if (companyCode && !config.headers["X-Company-Code"]) {
    config.headers["X-Company-Code"] = companyCode;
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    // A 401 from the login/recovery calls just means "wrong email or password" — the form shows that message itself.
    // Reloading the page here (as for an expired session) wiped the message, so a failed login looked like nothing happened.
    const isAuthForm = typeof error.config?.url === "string" && error.config.url.includes("/auth/");
    if (error.response?.status === 401 && !isAuthForm) {
      localStorage.removeItem("token");
      localStorage.removeItem("user");
      window.location.href = "/login";
    }
    return Promise.reject(error);
  }
);

export function getErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    return error.response?.data?.error ?? error.message;
  }
  return "حدث خطأ غير متوقع";
}
