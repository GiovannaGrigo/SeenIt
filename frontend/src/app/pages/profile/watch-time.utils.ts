export interface TimePart { value: number; unit: string; }

export function formatWatchTime(totalMinutes: number): TimePart[] {
  const minutes = Math.max(0, Math.floor(totalMinutes));
  const months = Math.floor(minutes / 43200);
  const hours = Math.floor((minutes % 43200) / 60);
  const remainingMinutes = minutes % 60;
  const part = (value: number, singular: string, plural: string): TimePart => ({
    value, unit: value === 1 ? singular : plural,
  });

  if (months > 0) {
    return [part(months, 'mês', 'meses'), part(hours, 'hora', 'horas')];
  }
  if (hours > 0) {
    return [part(hours, 'hora', 'horas'), part(remainingMinutes, 'minuto', 'minutos')];
  }
  return [part(remainingMinutes, 'minuto', 'minutos')];
}

export function describeMinutes(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const remainder = minutes % 60;
  return hours > 0 ? `${hours} h${remainder > 0 ? ` ${remainder} min` : ''}` : `${remainder} min`;
}

export function watchTimeBarHeight(minutes: number, maximum: number): number {
  return minutes > 0 ? Math.max(4, minutes / Math.max(60, maximum) * 90) : 3;
}
