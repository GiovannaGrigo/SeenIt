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
import { EpisodioAssistidoResponse } from "../../models/episodio-assistido.model";
import { EpisodioAssistidoService } from "../../core/episodio-assistido.service";
import { EpisodioModalComponent } from "../../shared/episodio-modal/episodio-modal.component";
import { NotificationService } from "../../core/notification.service";
import { SelectModule } from "primeng/select";

@Component({
  standalone: true,
  imports: [
    RouterLink,
    ReactiveFormsModule,
    EpisodioModalComponent,
    SelectModule,
  ],
  templateUrl: "./series-detail.component.html",
  styleUrl: "./series-detail.component.scss",
})
export class SeriesDetailComponent implements OnInit {
  private readonly episodioAssistidoService = inject(EpisodioAssistidoService);
  private readonly notification = inject(NotificationService);

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

  readonly statusOptions = [
    {
      label: "Para assistir",
      value: SeriesStatus.InList,
    },
    {
      label: "Assistindo",
      value: SeriesStatus.Watching,
    },
    {
      label: "Finalizada",
      value: SeriesStatus.Finished,
    },
  ];

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

    const jaEstavaNaLista = this.savedStatus() !== null;

    this.service.add(series, status).subscribe({
      next: () => {
        this.savedStatus.set(status);
        this.statusControl.reset();

        if (jaEstavaNaLista) {
          this.notification.success(
            `Status alterado para "${this.label(status)}".`,
            "Status atualizado",
          );
        } else {
          this.notification.success(
            `"${series.name}" foi adicionada à sua lista.`,
            "Série adicionada",
          );
        }
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

    const serie = this.series();

    if (!serie) {
      return;
    }

    this.service.getMySeries().subscribe({
      next: (series) => {
        const minhaSerie = series.find(
          (item) => item.externalSeriesId === serie.id,
        );

        this.savedStatus.set(minhaSerie?.status ?? null);
      },
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
