export function formatDateTime(value?: string | null) {
  if (!value) return '-';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '-';
  return date.toLocaleString('es-AR');
}

export function formatDate(value?: string | null) {
  if (!value) return '-';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '-';
  return date.toLocaleDateString('es-AR');
}

export function formatMeters(value?: number | null) {
  if (value == null) return '-';
  if (value >= 1000) return `${(value / 1000).toFixed(1)} km`;
  return `${Math.round(value)} m`;
}

export function formatCurrency(value?: number | null) {
  if (value == null) return '-';
  return value.toLocaleString('es-AR', { style: 'currency', currency: 'ARS' });
}

export function formatKg(value?: number | null) {
  if (value == null) return '-';
  return `${value.toLocaleString('es-AR', { maximumFractionDigits: 2 })} kg`;
}

export function shortId(value?: string | null) {
  if (!value) return '-';
  return value.length > 8 ? value.slice(0, 8) : value;
}
