import { Component, OnInit, computed, inject, signal } from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { forkJoin } from "rxjs";
import { SeriesService } from "../../core/series.service";
import {
  Episode,
  SeriesDetails,
  SeriesStatus,
} from "../../models/series.models";
import { FormControl, ReactiveFormsModule } from "@angular/forms";
import { EpisodioAssistidoResponse } from "src/app/models/episodio-assistido.model";
import { EpisodioAssistidoService } from "src/app/core/episodio-assistido.service";
import { EpisodioModalComponent } from "src/app/shared/episodio-modal/episodio-modal.component";

@Component({
  standalone: true,
  imports: [RouterLink, ReactiveFormsModule, EpisodioModalComponent],
  templateUrl: "./series-detail.component.html",
  styleUrl: "./series-detail.component.scss",
})
export class SeriesDetailComponent implements OnInit {
  private readonly episodioAssistidoService = inject(EpisodioAssistidoService);

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
  readonly episodioSelecionado = signal<Episode | null>(null);
  readonly episodiosAssistidos = signal<Map<number, EpisodioAssistidoResponse>>(
    new Map(),
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
          result.mine.find((item) => item.externalSeriesId === id)?.status ??
            null,
        );

        this.carregarEpisodiosAssistidos(id);

        this.loading.set(false);
      },

      error: (error) => {
        console.error("Erro ao carregar detalhes da série:", error);

        this.loading.set(false);
      },
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

  abrirModal(episodio: Episode): void {
    this.episodioSelecionado.set(episodio);
  }

  fecharModal(): void {
    this.episodioSelecionado.set(null);
  }

  foiAssistido(episodeId: number): boolean {
    return this.episodiosAssistidos().has(episodeId);
  }

  obterRegistro(episodeId: number): EpisodioAssistidoResponse | null {
    return this.episodiosAssistidos().get(episodeId) ?? null;
  }

  aoSalvarEpisodio(registro: EpisodioAssistidoResponse): void {
    this.episodiosAssistidos.update((atuais) => {
      const novos = new Map(atuais);

      novos.set(registro.externalEpisodeId, registro);

      return novos;
    });
  }

  private carregarEpisodiosAssistidos(seriesId: number): void {
    this.episodioAssistidoService.obterAssistidosPorSerie(seriesId).subscribe({
      next: (registros) => {
        this.episodiosAssistidos.set(
          new Map(
            registros.map((registro) => [registro.externalEpisodeId, registro]),
          ),
        );
      },
      error: (error) => {
        console.error("Erro ao carregar episódios assistidos:", error);
      },
    });
  }
}
