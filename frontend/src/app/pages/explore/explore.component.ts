import { Component, signal } from "@angular/core";
import {
  FormControl,
  ReactiveFormsModule,
  Validators,
  FormGroup,
} from "@angular/forms";
import { RouterLink } from "@angular/router";
import { finalize } from "rxjs";
import { SeriesService } from "../../core/series.service";
import { SeriesStatus, SeriesSummary } from "../../models/series.models";

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: "./explore.component.html",
  styleUrl: "./explore.component.scss",
})
export class ExploreComponent {
  readonly SeriesStatus = SeriesStatus;
  readonly query = new FormControl("", {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(2)],
  });
  readonly searchForm = new FormGroup({ query: this.query });
  readonly results = signal<SeriesSummary[]>([]);
  readonly loading = signal(false);
  readonly searched = signal(false);
  readonly saved = signal(new Set<number>());
  constructor(private readonly service: SeriesService) {}

  search(): void {
    if (this.query.invalid) {
      this.query.markAsTouched();
      return;
    }
    this.loading.set(true);
    this.searched.set(true);
    this.service
      .search(this.query.value.trim())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe((results) => this.results.set(results));
  }

  add(item: SeriesSummary): void {
    this.service.add(item, SeriesStatus.InList).subscribe({
      next: () => {
        this.saved.update((ids) => {
          const updated = new Set(ids);
          updated.add(item.id);
          return updated;
        });
      },
      error: (error) => {
        console.error("Erro ao adicionar série:", error);
      },
    });
  }
}
