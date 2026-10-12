import axios from "axios";
import { RestaurantApi } from "../api/services";
import type {
  FiscalPeriod,
  MenuItem,
  OfflineOrderInput,
  OfflineOrderResult,
  PaymentTerm,
  RestaurantOrderType,
  RestaurantTable,
  Warehouse,
} from "../api/types";
import { idb } from "./idb";

/** Everything the cashier needs to keep selling with no connection — refreshed every time the POS loads online. */
export interface PosSnapshot {
  savedAt: string;
  menu: MenuItem[];
  tables: RestaurantTable[];
  warehouses: Warehouse[];
  periods: FiscalPeriod[];
  activeShiftId: string | null;
  vatRate: number;
}

/** A ticket waiting on this device to be sent to the server. */
export interface OutboxTicket extends OfflineOrderInput {
  localNumber: number;
  /** Total shown to the cashier at the time (menu price + VAT) — the server recalculates from its own prices. */
  displayTotal: number;
  tableLabel: string | null;
  status: "pending" | "failed";
  error?: string;
}

const SNAPSHOT_KEY = "pos-snapshot";
const COUNTER_KEY = "pos-ticket-counter";

export async function saveSnapshot(snapshot: PosSnapshot): Promise<void> {
  try {
    await idb.set(SNAPSHOT_KEY, snapshot);
  } catch {
    // Offline mode is a bonus; never let a storage problem break normal selling.
  }
}

export async function loadSnapshot(): Promise<PosSnapshot | null> {
  try {
    return (await idb.get<PosSnapshot>(SNAPSHOT_KEY)) ?? null;
  } catch {
    return null;
  }
}

export async function listOutbox(): Promise<OutboxTicket[]> {
  try {
    const all = await idb.outboxAll<OutboxTicket>();
    return all.sort((a, b) => a.createdAtUtc.localeCompare(b.createdAtUtc));
  } catch {
    return [];
  }
}

export async function enqueueTicket(ticket: Omit<OutboxTicket, "clientId" | "localNumber" | "status" | "createdAtUtc">): Promise<OutboxTicket> {
  const counter = ((await idb.get<number>(COUNTER_KEY)) ?? 0) + 1;
  await idb.set(COUNTER_KEY, counter);
  const full: OutboxTicket = {
    ...ticket,
    clientId: crypto.randomUUID(),
    createdAtUtc: new Date().toISOString(),
    localNumber: counter,
    status: "pending",
  };
  await idb.outboxPut(full);
  return full;
}

export const discardTicket = (clientId: string) => idb.outboxDelete(clientId);

export interface SyncSummary {
  synced: number;
  failed: number;
  /** True when nothing could be sent because the server could not be reached — the tickets simply stay queued. */
  unreachable: boolean;
}

let syncing = false;

/** Sends every queued ticket. Tickets the server accepted are removed; ones it refused stay, marked failed with the reason. */
export async function syncOutbox(): Promise<SyncSummary> {
  if (syncing) return { synced: 0, failed: 0, unreachable: false };
  syncing = true;
  try {
    const queued = (await listOutbox()).filter((t) => t.status === "pending");
    if (queued.length === 0) return { synced: 0, failed: 0, unreachable: false };

    let results: OfflineOrderResult[];
    try {
      const payload = queued.map(({ localNumber: _l, displayTotal: _d, tableLabel: _t, status: _s, error: _e, ...ticket }) => ticket);
      results = (await RestaurantApi.syncOfflineOrders({ orders: payload })).data;
    } catch (err) {
      // No answer at all (or the session expired): keep everything queued and try again later.
      if (axios.isAxiosError(err) && err.response && err.response.status !== 401) {
        // The server answered with an error for the whole batch — surface it on every ticket instead of retrying forever.
        const message = (err.response.data as { error?: string } | undefined)?.error ?? err.message;
        await Promise.all(queued.map((t) => idb.outboxPut({ ...t, status: "failed", error: message })));
        return { synced: 0, failed: queued.length, unreachable: false };
      }
      return { synced: 0, failed: 0, unreachable: true };
    }

    let synced = 0;
    let failed = 0;
    for (const result of results) {
      const ticket = queued.find((t) => t.clientId === result.clientId);
      if (!ticket) continue;
      if (result.success) {
        synced++;
        await idb.outboxDelete(ticket.clientId);
      } else {
        failed++;
        await idb.outboxPut({ ...ticket, status: "failed", error: result.error ?? "" });
      }
    }
    return { synced, failed, unreachable: false };
  } finally {
    syncing = false;
  }
}

/** Puts a failed ticket back in line for another try (e.g. after stock was received). */
export async function retryTicket(ticket: OutboxTicket): Promise<void> {
  await idb.outboxPut({ ...ticket, status: "pending", error: undefined });
}

export type { PaymentTerm, RestaurantOrderType };
