import ManualProfitGrid from "../../components/ManualProfitGrid";
import { ManualProfitDimension } from "../../api/types";
import { useLanguage } from "../../i18n/LanguageContext";
import InfoTooltip from "../../components/InfoTooltip";

export default function BranchProfitabilityPage() {
  const { t } = useLanguage();

  return (
    <div>
      <div className="page-header">
        <h1>{t.accounting.branchProfitabilityTitle}<InfoTooltip text={t.accounting.branchProfitabilityIntro} /></h1>
      </div>
      <p className="text-muted">{t.accounting.branchProfitabilityIntro}</p>

      <ManualProfitGrid dimension={ManualProfitDimension.Branch} nameLabel={t.accounting.branchName} />
    </div>
  );
}
