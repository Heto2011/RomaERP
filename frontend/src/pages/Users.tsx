import { useEffect, useState } from "react";
import { EmployeesApi, UsersApi } from "../api/services";
import { AppRoles, type AppUser, type Employee } from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

export default function Users() {
  const { t } = useLanguage();
  const [users, setUsers] = useState<AppUser[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [fullName, setFullName] = useState("");
  const [roles, setRoles] = useState<string[]>([]);

  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingRoles, setEditingRoles] = useState<string[]>([]);

  async function load() {
    const [usersRes, employeesRes] = await Promise.all([UsersApi.getAll(), EmployeesApi.getAll()]);
    setUsers(usersRes.data);
    setEmployees(employeesRes.data);
  }

  useEffect(() => {
    load();
  }, []);

  function toggleRole(list: string[], role: string): string[] {
    return list.includes(role) ? list.filter((r) => r !== role) : [...list, role];
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await UsersApi.create({ email, password, fullName, roles });
      setShowForm(false);
      setEmail("");
      setPassword("");
      setFullName("");
      setRoles([]);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  function startEditRoles(user: AppUser) {
    setEditingId(user.id);
    setEditingRoles(user.roles);
  }

  async function saveRoles(id: string) {
    setError(null);
    try {
      await UsersApi.updateRoles(id, editingRoles);
      setEditingId(null);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function toggleActive(user: AppUser) {
    setError(null);
    try {
      if (user.isActive) await UsersApi.deactivate(user.id);
      else await UsersApi.activate(user.id);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function handleLinkEmployee(userId: string, employeeId: string) {
    setError(null);
    try {
      await UsersApi.linkEmployee(userId, employeeId || null);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>{t.users.title}</h1>
        <button className="btn" onClick={() => setShowForm((v) => !v)}>
          {showForm ? t.common.cancel : t.users.newUser}
        </button>
      </div>

      {error && <div className="alert-error">{error}</div>}

      {showForm && (
        <div className="card">
          <form onSubmit={handleSubmit}>
            <div className="form-grid">
              <div className="form-field">
                <label>{t.users.fullName}</label>
                <input value={fullName} onChange={(e) => setFullName(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.users.email}</label>
                <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
              </div>
              <div className="form-field">
                <label>{t.users.password}</label>
                <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required minLength={8} />
              </div>
            </div>
            <div className="form-field" style={{ marginTop: 14 }}>
              <label>{t.users.roles}</label>
              <div style={{ display: "flex", gap: 14, flexWrap: "wrap" }}>
                {AppRoles.map((role) => (
                  <label key={role} style={{ display: "flex", alignItems: "center", gap: 6, fontWeight: "normal" }}>
                    <input type="checkbox" checked={roles.includes(role)} onChange={() => setRoles((prev) => toggleRole(prev, role))} />
                    {t.roles[role]}
                  </label>
                ))}
              </div>
            </div>
            <button className="btn" type="submit" style={{ marginTop: 14 }}>
              {t.common.save}
            </button>
          </form>
        </div>
      )}

      <div className="card">
        <table>
          <thead>
            <tr>
              <th>{t.users.fullName}</th>
              <th>{t.users.email}</th>
              <th>{t.users.roles}</th>
              <th>{t.users.linkedEmployee}</th>
              <th>{t.common.status}</th>
              <th>{t.common.actions}</th>
            </tr>
          </thead>
          <tbody>
            {users.length === 0 && (
              <tr>
                <td colSpan={6} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
                  {t.common.noData}
                </td>
              </tr>
            )}
            {users.map((u) => (
              <tr key={u.id}>
                <td>{u.fullName}</td>
                <td>{u.email}</td>
                <td>
                  {editingId === u.id ? (
                    <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
                      {AppRoles.map((role) => (
                        <label key={role} style={{ display: "flex", alignItems: "center", gap: 4, fontWeight: "normal" }}>
                          <input type="checkbox" checked={editingRoles.includes(role)} onChange={() => setEditingRoles((prev) => toggleRole(prev, role))} />
                          {t.roles[role]}
                        </label>
                      ))}
                    </div>
                  ) : (
                    u.roles.map((r) => t.roles[r as keyof typeof t.roles] ?? r).join("، ")
                  )}
                </td>
                <td>
                  <select
                    value={u.employeeId ?? ""}
                    onChange={(e) => handleLinkEmployee(u.id, e.target.value)}
                  >
                    <option value="">{t.users.noLinkedEmployee}</option>
                    {employees
                      .filter((emp) => !emp.applicationUserId || emp.id === u.employeeId)
                      .map((emp) => (
                        <option key={emp.id} value={emp.id}>
                          {emp.employeeCode} - {emp.fullNameAr}
                        </option>
                      ))}
                  </select>
                </td>
                <td>
                  <span className={u.isActive ? "text-success" : "text-danger"}>
                    {u.isActive ? t.users.active : t.users.inactive}
                  </span>
                </td>
                <td style={{ display: "flex", gap: 6 }}>
                  {editingId === u.id ? (
                    <>
                      <button className="btn btn-sm" onClick={() => saveRoles(u.id)}>{t.users.saveRoles}</button>
                      <button className="btn btn-secondary btn-sm" onClick={() => setEditingId(null)}>{t.common.cancel}</button>
                    </>
                  ) : (
                    <>
                      <button className="btn btn-secondary btn-sm" onClick={() => startEditRoles(u)}>{t.users.editRoles}</button>
                      <button className="btn btn-secondary btn-sm" onClick={() => toggleActive(u)}>
                        {u.isActive ? t.users.deactivate : t.users.activate}
                      </button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
