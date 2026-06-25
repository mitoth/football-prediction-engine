export default function Logo({ className }: { className?: string }) {
  return (
    <svg
      className={className}
      viewBox="0 0 200 200"
      fill="none"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <circle cx="100" cy="100" r="80" stroke="currentColor" strokeWidth="9" />
      <path
        d="M 100,72 L 126.63,91.35 L 116.46,122.65 L 83.54,122.65 L 73.37,91.35 Z"
        stroke="currentColor"
        strokeWidth="9"
      />
      <path
        d="M 100,72 L 100,20 M 126.63,91.35 L 176.08,75.27 M 116.46,122.65 L 147.02,164.72 M 83.54,122.65 L 52.98,164.72 M 73.37,91.35 L 23.92,75.27"
        stroke="currentColor"
        strokeWidth="9"
      />
      <circle cx="100" cy="72" r="7" fill="#e9c34d" />
    </svg>
  )
}
