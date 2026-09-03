import { useEffect, useState } from "react";
import { EmployeesApi, UsersApi } from "../api/services";
import { AppRoles, ModulePermissions, type AppUser, type Employee } from "../api/types";
import { getErrorMessage } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";
import { bilingualName } from "../i18n/bilingual";

export default function Users() {
  const { t, lang } = useLanguage();
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

  const [editingModulesId, setEditingModulesId] = useState<string | null>(null);
  const [editingModules, setEditingModules] = useState<string[]>([]);

  const [pinEditingId, setPinEditingId] = useState<string | null>(null);
  const [pinValue, setPinValue] = useState("");

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

  function startEditModules(user: AppUser) {
    setEditingModulesId(user.id);
    setEditingModules(user.modules);
  }

  async function saveModules(id: string) {
    setError(null);
    try {
      await UsersApi.updateModules(id, editingModules);
      setEditingModulesId(null);
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

  async function saveNewPin(id: string) {
    setError(null);
    try {
      await UsersApi.setPosPin(id, pinValue);
      setPinEditingId(null);
      setPinValue("");
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }

  async function clearPin(id: string) {
    setError(null);
    try {
      await UsersApi.setPosPin(id, null);
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
              <th>{t.users.modules}</th>
              <th>{t.users.linkedEmployee}</th>
              <th>{t.users.posPin}</th>
              <th>{t.common.status}</th>
              <th>{t.common.actions}</th>
            </tr>
          </thead>
          <tbody>
            {users.length === 0 && (
              <tr>
                <td colSpan={8} className="text-muted" style={{ textAlign: "center", padding: 20 }}>
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
                  {editingModulesId === u.id ? (
                    <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
                      {ModulePermissions.map((module) => (
                        <label key={module} style={{ display: "flex", alignItems: "center", gap: 4, fontWeight: "normal" }}>
                          <input
                            type="checkbox"
                            checked={editingModules.includes(module)}
                            onChange={() => setEditingModules((prev) => toggleRole(prev, module))}
                          />
                          {t.modules[module]}
                        </label>
                      ))}
                    </div>
                  ) : u.modules.length > 0 ? (
                    u.modules.map((m) => t.modules[m as keyof typeof t.modules] ?? m).join("، ")
                  ) : (
                    <span className="text-muted">—</span>
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
                          {emp.employeeCode} - {bilingualName(emp.fullNameAr, emp.fullNameEn, lang)}
                        </option>
                      ))}
                  </select>
                </td>
                <td>
                  {pinEditingId === u.id ? (
                    <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
                      <input
                        style={{ width: 90 }}
                        value={pinValue}
                        onChange={(e) => setPinValue(e.target.value)}
                        placeholder={t.users.pinPlaceholder}
                        maxLength={6}
                        title={t.users.pinHint}
                      />
                      <button className="btn btn-sm" onClick={() => saveNewPin(u.id)}>{t.common.save}</button>
                      <button className="btn btn-secondary btn-sm" onClick={() => { setPinEditingId(null); setPinValue(""); }}>{t.common.cancel}</button>
                    </div>
                  ) : (
                    <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
                      <span className={u.hasPosPin ? "text-success" : "text-muted"}>
                        {u.hasPosPin ? t.users.posPinSet : t.users.posPinNotSet}
                      </span>
                      <button className="btn btn-secondary btn-sm" onClick={() => { setPinEditingId(u.id); setPinValue(""); }}>
                        {u.hasPosPin ? t.users.changePin : t.users.setPin}
                      </button>
                      {u.hasPosPin && (
                        <button className="btn btn-secondary btn-sm" onClick={() => clearPin(u.id)}>{t.users.clearPin}</button>
                      )}
                    </div>
                  )}
                </td>
                <td>
                  <span className={u.isActive ? "text-success" : "text-danger"}>
                    {u.isActive ? t.users.active : t.users.inactive}
                  </span>
                </td>
                <td style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                  {editingId === u.id ? (
                    <>
                      <button className="btn btn-sm" onClick={() => saveRoles(u.id)}>{t.users.saveRoles}</button>
                      <button className="btn btn-secondary btn-sm" onClick={() => setEditingId(null)}>{t.common.cancel}</button>
                    </>
                  ) : editingModulesId === u.id ? (
                    <>
                      <button className="btn btn-sm" onClick={() => saveModules(u.id)}>{t.users.saveModules}</button>
                      <button className="btn btn-secondary btn-sm" onClick={() => setEditingModulesId(null)}>{t.common.cancel}</button>
                    </>
                  ) : (
                    <>
                      <button className="btn btn-secondary btn-sm" onClick={() => startEditRoles(u)}>{t.users.editRoles}</button>
                      <button className="btn btn-secondary btn-sm" onClick={() => startEditModules(u)}>{t.users.editModules}</button>
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
