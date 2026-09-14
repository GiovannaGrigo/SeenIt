import {
  Component,
  HostListener,
  OnInit,
  computed,
  signal,
} from "@angular/core";

import { RouterLink } from "@angular/router";
import { finalize } from "rxjs";

import { SeriesService } from "../../core/series.service";
import { SeriesStatus, UserSeries } from "../../models/series.models";

@Component({
  standalone: true,
  imports: [RouterLink],
  templateUrl: "./library.component.html",
  styleUrl: "./library.component.scss",
})
export class LibraryComponent implements OnInit {
  readonly SeriesStatus = SeriesStatus;
  readonly items = signal<UserSeries[]>([]);
  readonly loading = signal(true);
  readonly filter = signal<SeriesStatus | null>(null);
  readonly menuAbertoId = signal<number | null>(null);
  readonly visibleItems = computed(() => {
    const filtroAtual = this.filter();

    if (filtroAtual === null) {
      return this.items();
    }

    return this.items().filter((item) => item.status === filtroAtual);
  });

  readonly counts = computed(() => ({
    all: this.items().length,

    inList: this.items().filter((item) => item.status === SeriesStatus.InList)
      .length,

    watching: this.items().filter(
      (item) => item.status === SeriesStatus.Watching,
    ).length,

    finished: this.items().filter(
      (item) => item.status === SeriesStatus.Finished,
    ).length,
  }));

  constructor(private readonly seriesService: SeriesService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);

    this.seriesService
      .getMySeries()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (items) => {
          this.items.set(items);
        },
        error: (error) => {
          console.error("Erro ao carregar a lista de séries:", error);
        },
      });
  }

  alternarMenu(seriesId: number, event: MouseEvent): void {
    event.preventDefault();
    event.stopPropagation();

    this.menuAbertoId.update((idAtual) =>
      idAtual === seriesId ? null : seriesId,
    );
  }

  alterarStatus(item: UserSeries, status: SeriesStatus): void {
    if (item.status === status) {
      this.menuAbertoId.set(null);
      return;
    }

    this.seriesService.updateStatus(item.externalSeriesId, status).subscribe({
      next: (updated) => {
        this.items.update((items) =>
          items.map((registro) =>
            registro.id === updated.id ? updated : registro,
          ),
        );

        this.menuAbertoId.set(null);
      },
      error: (error) => {
        console.error("Erro ao alterar o status:", error);
      },
    });
  }

  remove(item: UserSeries, event?: MouseEvent): void {
    event?.preventDefault();
    event?.stopPropagation();

    this.menuAbertoId.set(null);

    this.seriesService.remove(item.externalSeriesId).subscribe({
      next: () => {
        this.items.update((items) =>
          items.filter((registro) => registro.id !== item.id),
        );
      },
      error: (error) => {
        console.error("Erro ao remover a série:", error);
      },
    });
  }

  statusLabel(status: SeriesStatus): string {
    switch (status) {
      case SeriesStatus.InList:
        return "Para assistir";

      case SeriesStatus.Watching:
        return "Assistindo";

      case SeriesStatus.Finished:
        return "Finalizada";

      default:
        return "Status desconhecido";
    }
  }

  @HostListener("document:click")
  fecharMenu(): void {
    this.menuAbertoId.set(null);
  }

  @HostListener("document:keydown.escape")
  fecharMenuComEsc(): void {
    this.menuAbertoId.set(null);
  }
}
