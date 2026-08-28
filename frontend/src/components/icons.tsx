const common = {
  width: 18,
  height: 18,
  viewBox: "0 0 20 20",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: 1.6,
  strokeLinecap: "round" as const,
  strokeLinejoin: "round" as const,
};

export function IconGrid() {
  return (
    <svg {...common}>
      <rect x="2.5" y="2.5" width="6" height="6" rx="1" />
      <rect x="11.5" y="2.5" width="6" height="6" rx="1" />
      <rect x="2.5" y="11.5" width="6" height="6" rx="1" />
      <rect x="11.5" y="11.5" width="6" height="6" rx="1" />
    </svg>
  );
}

export function IconUser() {
  return (
    <svg {...common}>
      <circle cx="10" cy="6.5" r="3" />
      <path d="M3.5 17c0-3.5 3-5.5 6.5-5.5s6.5 2 6.5 5.5" />
    </svg>
  );
}

export function IconUsers() {
  return (
    <svg {...common}>
      <circle cx="7.5" cy="6.5" r="2.5" />
      <path d="M2.5 17c0-3 2.2-4.8 5-4.8s5 1.8 5 4.8" />
      <circle cx="14" cy="6.8" r="2" />
      <path d="M13 12.5c2.2.3 3.8 1.9 4.5 3.8" />
    </svg>
  );
}

export function IconChat() {
  return (
    <svg {...common}>
      <path d="M3 4.5h14v9H8l-3.5 3v-3H3z" />
    </svg>
  );
}

export function IconCheck() {
  return (
    <svg {...common}>
      <circle cx="10" cy="10" r="7" />
      <path d="M6.8 10.2l2.2 2.2 4.2-4.8" />
    </svg>
  );
}

export function IconRefresh() {
  return (
    <svg {...common}>
      <path d="M16 6.5A6.5 6.5 0 004.2 8" />
      <path d="M4 3.5V8h4.5" />
      <path d="M4 13.5A6.5 6.5 0 0015.8 12" />
      <path d="M16 16.5V12h-4.5" />
    </svg>
  );
}

export function IconList() {
  return (
    <svg {...common}>
      <line x1="4" y1="5.5" x2="16" y2="5.5" />
      <line x1="4" y1="10" x2="16" y2="10" />
      <line x1="4" y1="14.5" x2="16" y2="14.5" />
    </svg>
  );
}

export function IconWallet() {
  return (
    <svg {...common}>
      <rect x="2.5" y="5" width="15" height="11" rx="1.5" />
      <path d="M2.5 8.5h15" />
      <circle cx="14" cy="11.5" r="1" />
    </svg>
  );
}

export function IconBook() {
  return (
    <svg {...common}>
      <path d="M3 4.5c1.8-.8 4-.8 6 0v11c-2-.8-4.2-.8-6 0z" />
      <path d="M17 4.5c-1.8-.8-4-.8-6 0v11c2-.8 4.2-.8 6 0z" />
    </svg>
  );
}

export function IconBarChart() {
  return (
    <svg {...common}>
      <line x1="4" y1="17" x2="4" y2="10" />
      <line x1="10" y1="17" x2="10" y2="5" />
      <line x1="16" y1="17" x2="16" y2="12" />
    </svg>
  );
}

export function IconFile() {
  return (
    <svg {...common}>
      <path d="M6 2.5h6l3 3v12H6z" />
      <path d="M12 2.5v3h3" />
      <line x1="8" y1="10" x2="13" y2="10" />
      <line x1="8" y1="13" x2="13" y2="13" />
    </svg>
  );
}

export function IconCalendar() {
  return (
    <svg {...common}>
      <rect x="2.5" y="4" width="15" height="13" rx="1.5" />
      <line x1="2.5" y1="8" x2="17.5" y2="8" />
      <line x1="6" y1="2" x2="6" y2="5.5" />
      <line x1="14" y1="2" x2="14" y2="5.5" />
    </svg>
  );
}

export function IconBox() {
  return (
    <svg {...common}>
      <path d="M10 2.5l7 3.5v8L10 17.5 3 14v-8z" />
      <path d="M3 6l7 3.5 7-3.5" />
      <line x1="10" y1="9.5" x2="10" y2="17.5" />
    </svg>
  );
}

export function IconTrendDown() {
  return (
    <svg {...common}>
      <polyline points="3.5,5.5 8,10.5 11.5,7.5 16.5,14" />
      <polyline points="12,14 16.5,14 16.5,9.5" />
    </svg>
  );
}

export function IconClock() {
  return (
    <svg {...common}>
      <circle cx="10" cy="10" r="7.2" />
      <path d="M10 6v4l3 2" />
    </svg>
  );
}

export function IconTruck() {
  return (
    <svg {...common}>
      <rect x="2" y="6.5" width="9" height="7" />
      <path d="M11 9.5h3.5L17 12v1.5h-6z" />
      <circle cx="6" cy="15" r="1.4" />
      <circle cx="14.5" cy="15" r="1.4" />
    </svg>
  );
}

export function IconCart() {
  return (
    <svg {...common}>
      <path d="M3 4h2l1.6 9.2h8.4L16.5 7H6" />
      <circle cx="8" cy="16.5" r="1.1" />
      <circle cx="14" cy="16.5" r="1.1" />
    </svg>
  );
}

export function IconBuilding() {
  return (
    <svg {...common}>
      <rect x="4.5" y="2.5" width="11" height="15" />
      <line x1="7" y1="5.5" x2="7" y2="5.5" />
      <line x1="7" y1="8" x2="7" y2="8" />
      <line x1="10" y1="5.5" x2="13" y2="5.5" />
      <line x1="7" y1="11" x2="13" y2="11" />
      <line x1="8.5" y1="17.5" x2="8.5" y2="14" />
      <line x1="11.5" y1="17.5" x2="11.5" y2="14" />
    </svg>
  );
}

export function IconBriefcase() {
  return (
    <svg {...common}>
      <rect x="2.5" y="6.5" width="15" height="10" rx="1.5" />
      <path d="M7 6.5V4.5h6v2" />
      <line x1="2.5" y1="11" x2="17.5" y2="11" />
    </svg>
  );
}

export function IconDollar() {
  return (
    <svg {...common}>
      <circle cx="10" cy="10" r="7.2" />
      <path d="M12.2 7.3c-.4-.6-1.2-1-2.2-1-1.4 0-2.5.8-2.5 1.9 0 2.7 4.7 1.4 4.7 3.9 0 1.1-1.1 1.9-2.5 1.9-1 0-1.8-.4-2.2-1" />
      <line x1="10" y1="5" x2="10" y2="15" />
    </svg>
  );
}

export function IconArchive() {
  return (
    <svg {...common}>
      <rect x="2.5" y="3.5" width="15" height="3.5" rx="1" />
      <path d="M3.5 7v8a1 1 0 001 1h11a1 1 0 001-1V7" />
      <line x1="8" y1="10.5" x2="12" y2="10.5" />
    </svg>
  );
}

export function IconSwap() {
  return (
    <svg {...common}>
      <path d="M4 6.5h11l-3-3" />
      <path d="M16 13.5H5l3 3" />
    </svg>
  );
}

export function IconEdit() {
  return (
    <svg {...common}>
      <path d="M12.5 3.5l4 4-9.5 9.5H3v-4z" />
    </svg>
  );
}

export function IconShield() {
  return (
    <svg {...common}>
      <path d="M10 2.5l6.5 2.3v5c0 4-2.7 6.7-6.5 7.7-3.8-1-6.5-3.7-6.5-7.7v-5z" />
      <path d="M7.3 10l1.9 1.9 3.5-4" />
    </svg>
  );
}

export function IconMenuToggle({ collapsed }: { collapsed: boolean }) {
  return (
    <svg {...common} width="20" height="20">
      {collapsed ? (
        <>
          <line x1="4" y1="6" x2="16" y2="6" />
          <line x1="4" y1="10" x2="16" y2="10" />
          <line x1="4" y1="14" x2="16" y2="14" />
        </>
      ) : (
        <>
          <line x1="5" y1="5" x2="15" y2="15" />
          <line x1="15" y1="5" x2="5" y2="15" />
        </>
      )}
    </svg>
  );
}
