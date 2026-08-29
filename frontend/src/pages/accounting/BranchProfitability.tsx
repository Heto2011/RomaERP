import ManualProfitGrid from "../../components/ManualProfitGrid";
import { ManualProfitDimension } from "../../api/types";
import { useLanguage } from "../../i18n/LanguageContext";

export default function BranchProfitabilityPage() {
  const { t } = useLanguage();

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.branchProfitabilityTitle}</h1>
      </div>
      <p className="text-muted">{t.accounting.branchProfitabilityIntro}</p>

      <ManualProfitGrid dimension={ManualProfitDimension.Branch} nameLabel={t.accounting.branchName} />
    </div>
  );
}
