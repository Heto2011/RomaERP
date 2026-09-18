function csvEscape(value: string): string {
  if (/[",\n\r]/.test(value)) {
    return `"${value.replace(/"/g, '""')}"`;
  }
  return value;
}

function cellText(cell: Element): string {
  return cell.textContent?.trim().replace(/\s+/g, " ") ?? "";
}

export function exportElementTableToCsv(container: HTMLElement, fileName: string) {
  const tables = container.querySelectorAll("table");
  if (tables.length === 0) return;

  const rows: string[] = [];
  tables.forEach((table, tableIndex) => {
    if (tableIndex > 0) rows.push("");
    table.querySelectorAll("tr").forEach((row) => {
      const cells = Array.from(row.querySelectorAll("th, td")).map((cell) => csvEscape(cellText(cell)));
      rows.push(cells.join(","));
    });
  });

  // A UTF-8 BOM so Excel detects the encoding and renders Arabic text correctly.
  const blob = new Blob(["﻿" + rows.join("\r\n")], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `${fileName}.csv`;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

export function printElement(container: HTMLElement) {
  container.classList.add("print-report-target");
  document.body.classList.add("printing-report");

  const cleanup = () => {
    container.classList.remove("print-report-target");
    document.body.classList.remove("printing-report");
    window.removeEventListener("afterprint", cleanup);
  };
  window.addEventListener("afterprint", cleanup);

  window.print();
}
