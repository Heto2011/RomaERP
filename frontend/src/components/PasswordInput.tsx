import { useState, type CSSProperties } from "react";
import { IconEye, IconEyeOff } from "./icons";

interface PasswordInputProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  minLength?: number;
  required?: boolean;
  style?: CSSProperties;
}

/// <summary>A password field with an eye icon to reveal what you typed — plain password inputs make it easy
/// to mistype a new password with no way to check it before saving.</summary>
export default function PasswordInput({ value, onChange, placeholder, minLength, required, style }: PasswordInputProps) {
  const [visible, setVisible] = useState(false);

  return (
    <div style={{ position: "relative", display: "inline-flex", alignItems: "center", ...style }}>
      <input
        type={visible ? "text" : "password"}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        minLength={minLength}
        required={required}
        style={{ width: "100%", paddingInlineEnd: 32 }}
      />
      <button
        type="button"
        onClick={() => setVisible((v) => !v)}
        tabIndex={-1}
        style={{
          position: "absolute",
          insetInlineEnd: 6,
          background: "none",
          border: "none",
          padding: 0,
          cursor: "pointer",
          display: "flex",
          color: "var(--color-muted)",
        }}
      >
        {visible ? <IconEyeOff /> : <IconEye />}
      </button>
    </div>
  );
}
