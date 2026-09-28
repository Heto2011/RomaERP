import { Component, type ErrorInfo, type ReactNode } from "react";

interface Props {
  children: ReactNode;
}

interface State {
  hasError: boolean;
}

/** Catches a render-time crash anywhere below it and shows a recoverable screen instead of a blank
 * white page. Must be a class component — componentDidCatch has no hook equivalent. */
export default class ErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("Unhandled render error:", error, info.componentStack);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div style={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", minHeight: "60vh", padding: 24, textAlign: "center", gap: 14 }}>
          <div style={{ fontSize: 40 }}>⚠️</div>
          <h2 style={{ margin: 0 }}>حصلت مشكلة غير متوقعة</h2>
          <p className="text-muted" style={{ maxWidth: 420 }}>
            الصفحة دي واجهت خطأ. جرب ترجع للرئيسية أو تحدّث الصفحة — بياناتك محفوظة وآمنة.
          </p>
          <div style={{ display: "flex", gap: 10 }}>
            <button className="btn" onClick={() => window.location.assign("/")}>الرجوع للرئيسية</button>
            <button className="btn btn-secondary" onClick={() => window.location.reload()}>تحديث الصفحة</button>
          </div>
        </div>
      );
    }
    return this.props.children;
  }
}
