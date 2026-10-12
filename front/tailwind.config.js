/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  theme: {
    extend: {
      colors: {
        brand: {
          50: "#eff6ff",
          100: "#dbeafe",
          200: "#bfdbfe",
          300: "#93c5fd",
          400: "#60a5fa",
          500: "#3b82f6",
          600: "#2563eb",
          700: "#1d4ed8",
          800: "#1e40af",
          900: "#1e3a8a",
        },
        surface: {
          DEFAULT: "#ffffff",
          muted: "#f8fafc",
        },
        foreground: "#0f172a",
        muted: "#64748b",
        success: {
          DEFAULT: "#16a34a",
          soft: "#dcfce7",
          strong: "#15803d",
        },
        warning: {
          DEFAULT: "#d97706",
          soft: "#fef3c7",
          strong: "#b45309",
        },
        danger: {
          DEFAULT: "#dc2626",
          soft: "#fee2e2",
          strong: "#b91c1c",
        },
        info: {
          DEFAULT: "#0284c7",
          soft: "#e0f2fe",
          strong: "#0369a1",
        },
      },
      borderColor: {
        DEFAULT: "#e2e8f0",
      },
      boxShadow: {
        card: "0 1px 2px 0 rgba(15, 23, 42, 0.06), 0 1px 3px 0 rgba(15, 23, 42, 0.05)",
      },
      fontFamily: {
        sans: ["system-ui", "Segoe UI", "Roboto", "sans-serif"],
      },
    },
  },
  plugins: [],
}
