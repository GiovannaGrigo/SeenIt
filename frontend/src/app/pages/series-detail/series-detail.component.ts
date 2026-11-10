import { Component, OnInit, computed, inject, signal } from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { catchError, finalize, forkJoin, Observable, of, tap } from "rxjs";
import { SeriesService } from "../../core/series.service";
import {
  Episode,
  SeriesDetails,
  SeriesStatus,
} from "../../models/series.models";
import { FormControl, ReactiveFormsModule } from "@angular/forms";
import { EpisodioAssistidoResponse, MarcarEpisodioAssistidoRequest } from "../../models/episodio-assistido.model";
import { EpisodioAssistidoService } from "../../core/episodio-assistido.service";
import { EpisodioModalComponent } from "../../shared/episodio-modal/episodio-modal.component";
import { NotificationService } from "../../core/notification.service";
import { SelectModule } from "primeng/select";
import { httpErrorMessage } from '../../core/http-error';

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
  readonly loading = signal(false);
  readonly readErrors = signal<string[]>([]);
  readonly assistidosError = signal<string | null>(null);
  readonly assistidosReady = signal(false);
  readonly listReady = signal(false);
  readonly episodesReady = signal(false);
  readonly savingStatus = signal(false);
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

  readonly episodioSalvando = signal<number | null>(null);
  readonly temporadaSalvando = signal<number | null>(null);
  readonly desmarcandoTemporada = signal(false);
  readonly carregandoAssistidos = signal(false);
  readonly temporadaAssistida = computed(() =>
    this.seasonEpisodes().length > 0 &&
    this.seasonEpisodes().every((ep) => this.foiAssistido(ep.id)),
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
    this.load();
  }

  load(): void {
    if (this.loading()) return;
    this.loading.set(true);
    this.readErrors.set([]);
    const id = Number(this.route.snapshot.paramMap.get("id"));
    // Catch each read independently; one 429 must not discard other responses.
    const read = <T>(source: Observable<T>, success: (value: T) => void, failed?: () => void) => source.pipe(
      tap(success),
      catchError(error => {
        failed?.();
        this.readErrors.update(errors => [...errors, httpErrorMessage(error)]);
        return of(null);
      }),
    );
    forkJoin({
      series: read(this.service.getDetails(id, true), value => this.series.set(value)),
      episodes: read(this.service.getEpisodes(id, true), value => {
        this.episodes.set(value);
        this.episodesReady.set(true);
        if (!this.seasons().includes(this.selectedSeason())) this.selectedSeason.set(this.seasons()[0] ?? 1);
      }, () => this.episodesReady.set(false)),
      mine: read(this.service.getMySeries(undefined, true), value => {
        this.savedStatus.set(value.find(item => item.externalSeriesId === id)?.status ?? null);
        this.listReady.set(true);
      }, () => this.listReady.set(false)),
    }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: () => this.carregarEpisodiosAssistidos(id),
    });
  }

  save(status: SeriesStatus): void {
    const series = this.series();

    if (!series || !this.listReady() || this.savingStatus()) {
      return;
    }

    const jaEstavaNaLista = this.savedStatus() !== null;

    this.savingStatus.set(true);
    this.service.add(series, status).pipe(finalize(() => this.savingStatus.set(false))).subscribe({
      next: () => {
        this.savedStatus.set(status);
        this.statusControl.reset();

        if (status === SeriesStatus.Finished) {
          this.carregarEpisodiosAssistidos(series.id);
        }

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

      error: () => {}, // The interceptor owns write-error notifications.
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
    if (this.temporadaSalvando() !== null || !this.assistidosReady() || this.carregandoAssistidos() || this.assistidosError()) return;
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

    this.atualizarStatusSalvo();
  }

  private atualizarStatusSalvo(): void {
    const serie = this.series();

    if (!serie) {
      return;
    }

    this.service.getMySeries(undefined, true).subscribe({
      next: (series) => {
        const minhaSerie = series.find(
          (item) => item.externalSeriesId === serie.id,
        );

        this.savedStatus.set(minhaSerie?.status ?? null);
        this.listReady.set(true);
      },
      error: error => {
        this.listReady.set(false);
        this.readErrors.update(errors => [...errors, httpErrorMessage(error)]);
      },
    });
  }

  private carregarEpisodiosAssistidos(seriesId: number): void {
    if (this.carregandoAssistidos()) return;
    this.carregandoAssistidos.set(true);
    this.assistidosError.set(null);
    this.episodioAssistidoService.obterAssistidosPorSerie(seriesId, true)
      .pipe(finalize(() => this.carregandoAssistidos.set(false))).subscribe({
      next: (registros) => {
        this.episodiosAssistidos.set(
          new Map(
            registros.map((registro) => [registro.externalEpisodeId, registro]),
          ),
        );
        this.assistidosReady.set(true);
      },
      error: error => {
        this.assistidosReady.set(false);
        this.assistidosError.set(httpErrorMessage(error));
      },
    });
  }

  retryAssistidos(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.carregarEpisodiosAssistidos(id);
  }

  alternarTemporadaAssistida(): void {
    const serie = this.series();
    const temporada = this.selectedSeason();
    if (!serie || !this.assistidosReady() || this.carregandoAssistidos() || this.assistidosError() || !this.episodesReady() ||
        !this.seasonEpisodes().length || this.temporadaSalvando() !== null ||
        this.episodioSalvando() !== null || this.episodioSelecionado()) return;

    const desmarcar = this.temporadaAssistida();
    this.desmarcandoTemporada.set(desmarcar);
    this.temporadaSalvando.set(temporada);
    const operacao = desmarcar
      ? this.episodioAssistidoService.desmarcarTemporada(serie.id, temporada)
      : this.episodioAssistidoService.marcarTemporadaComoAssistida(serie.id, temporada);
    operacao.pipe(finalize(() => this.temporadaSalvando.set(null)))
      .subscribe({
        next: (registros) => {
          this.episodiosAssistidos.set(new Map(
            registros.map((registro) => [registro.externalEpisodeId, registro]),
          ));
          this.temporadaSalvando.set(null);
          this.atualizarStatusSalvo();
          this.notification.success(
            desmarcar
              ? `Temporada ${temporada} desmarcada. Os episódios voltaram a ficar pendentes.`
              : `Temporada ${temporada} marcada como assistida.`,
            desmarcar ? "Temporada desmarcada" : "Temporada assistida",
          );
        },
        error: () => {
          this.temporadaSalvando.set(null);
          this.carregarEpisodiosAssistidos(serie.id);
          this.atualizarStatusSalvo();
        },
      });
  }

  marcarRapidamenteComoAssistido(episodio: Episode, event: MouseEvent): void {
    event.stopPropagation();

    if (!this.assistidosReady() || this.foiAssistido(episodio.id) || this.episodioSalvando() !== null || this.temporadaSalvando() !== null || this.carregandoAssistidos() || this.assistidosError()) {
      return;
    }

    const serie = this.series();

    if (!serie) {
      return;
    }

    this.episodioSalvando.set(episodio.id);

    const request: MarcarEpisodioAssistidoRequest = {
      externalSeriesId: serie.id,
      temporada: episodio.season,
      numeroEpisodio: episodio.number,
      nomeEpisodio: episodio.name,
      sentimentos: [],
      externalCharacterId: 0,
      personagemFavoritoNome: "",
    };

    this.episodioAssistidoService
      .marcarComoAssistido(episodio.id, request)
      .pipe(finalize(() => this.episodioSalvando.set(null)))
      .subscribe({
        next: (registro) => {
          this.episodioSalvando.set(null);

          this.aoSalvarEpisodio(registro);

          this.notification.success(
            `"${episodio.name}" foi marcado como assistido.`,
            "Episódio assistido",
          );
        },

        error: () => {}, // Finalize releases the action; the interceptor displays the error.
      });
  }
}
