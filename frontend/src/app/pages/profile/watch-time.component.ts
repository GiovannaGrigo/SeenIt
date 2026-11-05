import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { ProfileService, WatchTimeResponse } from '../../core/profile.service';
import { describeMinutes, formatWatchTime, watchTimeBarHeight } from './watch-time.utils';

@Component({
  selector: 'app-watch-time',
  standalone: true,
  templateUrl: './watch-time.component.html',
  styleUrl: './watch-time.component.scss',
})
export class WatchTimeComponent implements OnInit {
  private readonly profileService = inject(ProfileService);
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly data = signal<WatchTimeResponse | null>(null);
  readonly total = computed(() => formatWatchTime(this.data()?.totalMinutes ?? 0));
  readonly fullTotal = computed(() => describeMinutes(this.data()?.totalMinutes ?? 0));
  readonly hoursInChart = computed(() => this.data()?.days.some(day => day.minutes >= 60) ?? false);
  readonly days = computed(() => {
    const data = this.data();
    if (!data) return [];
    const maximum = Math.max(60, ...data.days.map(day => day.minutes));
    const names = ['Segunda-feira', 'Terça-feira', 'Quarta-feira', 'Quinta-feira', 'Sexta-feira', 'Sábado', 'Domingo'];
    return data.days.map((day, index) => ({
      ...day,
      label: names[index][0],
      description: `${names[index]}, ${day.date.split('-').reverse().join('/')}: ${describeMinutes(day.minutes)}`,
      height: watchTimeBarHeight(day.minutes, maximum),
      isToday: day.date === data.today,
    }));
  });

  ngOnInit(): void { this.load(); }

  load(): void {
    if (!this.loading() && !this.error()) return;
    this.loading.set(true);
    this.error.set(false);
    const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
    this.profileService.getWatchTime(timeZone)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: data => this.data.set(data),
        error: () => this.error.set(true),
      });
  }
}
