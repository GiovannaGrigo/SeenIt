import { Component, OnInit, computed, signal } from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { forkJoin } from "rxjs";
import { SeriesService } from "../../core/series.service";
import {
  Episode,
  SeriesDetails,
  SeriesStatus,
} from "../../models/series.models";
import { FormControl, ReactiveFormsModule } from "@angular/forms";

@Component({
  standalone: true,
  imports: [RouterLink, ReactiveFormsModule],
  templateUrl: "./series-detail.component.html",
  styleUrl: "./series-detail.component.scss",
})
export class SeriesDetailComponent implements OnInit {
  readonly SeriesStatus = SeriesStatus;
  readonly series = signal<SeriesDetails | null>(null);
  readonly episodes = signal<Episode[]>([]);
  readonly selectedSeason = signal(1);
  readonly savedStatus = signal<SeriesStatus | null>(null);
  readonly loading = signal(true);
  readonly seasons = computed(() =>
    [...new Set(this.episodes().map((x) => x.season))].sort((a, b) => a - b),
  );
  readonly seasonEpisodes = computed(() =>
    this.episodes().filter((x) => x.season === this.selectedSeason()),
  );
  readonly statusControl = new FormControl<SeriesStatus | null>(null);

  constructor(
    private route: ActivatedRoute,
    private service: SeriesService,
  ) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get("id"));
    forkJoin({
      series: this.service.getDetails(id),
      episodes: this.service.getEpisodes(id),
      mine: this.service.getMySeries(),
    }).subscribe({
      next: (result) => {
        this.series.set(result.series);
        this.episodes.set(result.episodes);
        this.selectedSeason.set(this.seasons()[0] ?? 1);
        this.savedStatus.set(
          result.mine.find((x) => x.externalSeriesId === id)?.status ?? null,
        );
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  save(status: SeriesStatus): void {
    const series = this.series();

    if (!series) {
      return;
    }

    this.service.add(series, status).subscribe({
      next: () => {
        this.savedStatus.set(status);
        this.statusControl.reset();
      },
      error: (error) => {
        console.error("Erro ao atualizar status:", error);
      },
    });
  }

  label(status: SeriesStatus | null): string {
    return status === SeriesStatus.Watching
      ? "Assistindo"
      : status === SeriesStatus.Finished
        ? "Finalizada"
        : "Para assistir";
  }

  episodeCode(ep: Episode): string {
    return `T${String(ep.season).padStart(2, "0")}E${String(ep.number).padStart(2, "0")}`;
  }
}
