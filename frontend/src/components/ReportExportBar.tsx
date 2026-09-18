import type { RefObject } from "react";
import { useLanguage } from "../i18n/LanguageContext";
import { exportElementTableToCsv, printElement } from "../utils/reportExport";

export default function ReportExportBar({
  targetRef,
  fileName,
}: {
  targetRef: RefObject<HTMLElement | null>;
  fileName: string;
}) {
  const { t } = useLanguage();

  return (
    <div className="report-export-bar">
      <button
        type="button"
        className="btn btn-secondary btn-sm"
        onClick={() => targetRef.current && exportElementTableToCsv(targetRef.current, fileName)}
      >
        {t.common.exportExcel}
      </button>
      <button
        type="button"
        className="btn btn-secondary btn-sm"
        onClick={() => targetRef.current && printElement(targetRef.current)}
      >
        {t.common.exportPdf}
      </button>
    </div>
  );
}
