import { Component, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { SeriesService } from '../../core/series.service';
import { SeriesStatus, SeriesSummary } from '../../models/series.models';

@Component({ standalone: true, imports: [ReactiveFormsModule, RouterLink], templateUrl: './explore.component.html', styleUrl: './explore.component.scss' })
export class ExploreComponent {
  readonly SeriesStatus = SeriesStatus;
  readonly query = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2)] });
  readonly results = signal<SeriesSummary[]>([]);
  readonly loading = signal(false);
  readonly searched = signal(false);
  readonly saved = signal(new Map<number, SeriesStatus>());
  constructor(private readonly service: SeriesService) {}
  search(): void {
    if (this.query.invalid) { this.query.markAsTouched(); return; }
    this.loading.set(true); this.searched.set(true);
    this.service.search(this.query.value.trim()).pipe(finalize(() => this.loading.set(false))).subscribe(results => this.results.set(results));
  }
  add(item: SeriesSummary, status: SeriesStatus): void {
    this.service.add(item, status).subscribe(() => this.saved.update(map => new Map(map).set(item.id, status)));
  }
  label(status?: SeriesStatus): string { return status === SeriesStatus.Watching ? 'Assistindo' : status === SeriesStatus.Finished ? 'Finalizada' : 'Na lista'; }
}
