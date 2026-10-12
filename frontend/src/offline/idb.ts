// A tiny promise wrapper over IndexedDB — just enough for the POS offline snapshot and ticket queue.
const DB_NAME = "roma-pos-offline";
const DB_VERSION = 1;

function open(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);
    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains("kv")) db.createObjectStore("kv");
      if (!db.objectStoreNames.contains("outbox")) db.createObjectStore("outbox", { keyPath: "clientId" });
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function run<T>(store: "kv" | "outbox", mode: IDBTransactionMode, action: (s: IDBObjectStore) => IDBRequest<T>): Promise<T> {
  const db = await open();
  return new Promise<T>((resolve, reject) => {
    const tx = db.transaction(store, mode);
    const request = action(tx.objectStore(store));
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
    tx.oncomplete = () => db.close();
    tx.onabort = () => {
      db.close();
      reject(tx.error);
    };
  });
}

export const idb = {
  get: <T,>(key: string) => run<T | undefined>("kv", "readonly", (s) => s.get(key)),
  set: (key: string, value: unknown) => run("kv", "readwrite", (s) => s.put(value, key)),
  outboxAll: <T,>() => run<T[]>("outbox", "readonly", (s) => s.getAll()),
  outboxPut: (value: unknown) => run("outbox", "readwrite", (s) => s.put(value)),
  outboxDelete: (clientId: string) => run("outbox", "readwrite", (s) => s.delete(clientId)),
};
