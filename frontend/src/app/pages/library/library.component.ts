import { Component, OnInit, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { SeriesService } from '../../core/series.service';
import { SeriesStatus, UserSeries } from '../../models/series.models';

@Component({ standalone: true, imports: [RouterLink], templateUrl: './library.component.html', styleUrl: './library.component.scss' })
export class LibraryComponent implements OnInit {
  readonly SeriesStatus = SeriesStatus;
  readonly items = signal<UserSeries[]>([]);
  readonly loading = signal(true);
  readonly filter = signal<SeriesStatus | null>(null);
  readonly visibleItems = computed(() => this.filter() ? this.items().filter(x => x.status === this.filter()) : this.items());
  readonly counts = computed(() => ({
    all: this.items().length,
    inList: this.items().filter(x => x.status === SeriesStatus.InList).length,
    watching: this.items().filter(x => x.status === SeriesStatus.Watching).length,
    finished: this.items().filter(x => x.status === SeriesStatus.Finished).length
  }));

  constructor(private readonly seriesService: SeriesService) {}
  ngOnInit(): void { this.load(); }
  load(): void {
    this.loading.set(true);
    this.seriesService.getMySeries().pipe(finalize(() => this.loading.set(false))).subscribe(items => this.items.set(items));
  }
  setStatus(item: UserSeries, status: SeriesStatus): void {
    this.seriesService.updateStatus(item.externalSeriesId, status).subscribe(updated =>
      this.items.update(items => items.map(x => x.id === updated.id ? updated : x)));
  }
  remove(item: UserSeries): void {
    this.seriesService.remove(item.externalSeriesId).subscribe(() => this.items.update(items => items.filter(x => x.id !== item.id)));
  }
  statusLabel(status: SeriesStatus): string {
    return ({ [SeriesStatus.InList]: 'Para assistir', [SeriesStatus.Watching]: 'Assistindo', [SeriesStatus.Finished]: 'Finalizada' })[status];
  }
}
