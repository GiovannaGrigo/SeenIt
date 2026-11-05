import {
  Component,
  ElementRef,
  HostListener,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
  afterRenderEffect,
} from "@angular/core";

import { RouterLink } from "@angular/router";
import { finalize } from "rxjs";

import { SeriesService } from "../../core/series.service";
import { SeriesStatus, UserSeries } from "../../models/series.models";
import { NotificationService } from "../../core/notification.service";

@Component({
  standalone: true,
  imports: [RouterLink],
  templateUrl: "./library.component.html",
  styleUrl: "./library.component.scss",
})
export class LibraryComponent implements OnInit {
  private readonly notification = inject(NotificationService);
  readonly SeriesStatus = SeriesStatus;
  readonly items = signal<UserSeries[]>([]);
  readonly loading = signal(true);
  readonly filter = signal<SeriesStatus | null>(null);
  readonly favoriteItems = computed(() => this.items().filter((item) => item.isFavorite));
  readonly savingFavorites = signal<Set<number>>(new Set());
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

  private readonly mySeriesRow = viewChild<ElementRef<HTMLElement>>('mySeriesRow');
  private readonly favoriteSeriesRow = viewChild<ElementRef<HTMLElement>>('favoriteSeriesRow');
  readonly listNavigation = signal({ previous: false, next: false });
  readonly favoriteNavigation = signal({ previous: false, next: false });

  constructor(private readonly seriesService: SeriesService) {
    afterRenderEffect((onCleanup) => {
      this.visibleItems();
      this.favoriteItems();
      const rows = [
        { element: this.mySeriesRow()?.nativeElement, state: this.listNavigation },
        { element: this.favoriteSeriesRow()?.nativeElement, state: this.favoriteNavigation },
      ];
      const update = () => {
        for (const { element, state } of rows) {
          const maxScroll = element ? element.scrollWidth - element.clientWidth : 0;
          state.set({
            previous: !!element && maxScroll > 1 && element.scrollLeft > 1,
            next: !!element && maxScroll > 1 && element.scrollLeft < maxScroll - 1,
          });
        }
      };
      const observer = new ResizeObserver(update);
      for (const { element } of rows) {
        if (!element) continue;
        observer.observe(element);
        // Card widths also change with the viewport, even if the row width stays fixed.
        for (const child of Array.from(element.children)) observer.observe(child);
        element.addEventListener('scroll', update, { passive: true });
      }
      update();
      onCleanup(() => {
        observer.disconnect();
        for (const { element } of rows) element?.removeEventListener('scroll', update);
      });
    });
  }

  ngOnInit(): void {
    this.load();
  }

  scrollSeries(container: HTMLElement, direction: -1 | 1): void {
    this.fecharMenu();
    container.scrollBy({
      left: direction * container.clientWidth,
      behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth',
    });
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

        this.notification.success(
          `Status alterado para "${this.statusLabel(status)}".`,
          "Status atualizado",
        );
      },

      error: (error) => {
        console.error("Erro ao alterar o status:", error);
      },
    });
  }

  toggleFavorite(item: UserSeries, event: MouseEvent): void {
    event.preventDefault();
    event.stopPropagation();
    if (this.savingFavorites().has(item.externalSeriesId)) return;

    this.savingFavorites.update((ids) => new Set(ids).add(item.externalSeriesId));
    this.seriesService.updateFavorite(item.externalSeriesId, !item.isFavorite)
      .pipe(finalize(() => this.savingFavorites.update((ids) => {
        const next = new Set(ids);
        next.delete(item.externalSeriesId);
        return next;
      })))
      .subscribe({
        next: (updated) => {
          this.items.update((items) => items.map((entry) =>
            entry.id === updated.id ? { ...entry, isFavorite: updated.isFavorite, updatedAtUtc: updated.updatedAtUtc } : entry));
          this.notification.success(
            `"${item.name}" ${updated.isFavorite ? 'foi adicionada aos' : 'foi removida dos'} favoritos.`,
            'Favoritos atualizados',
          );
        },
        error: (error) => console.error('Erro ao atualizar favoritos:', error),
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

        this.notification.success(
          `"${item.name}" foi removida da sua lista.`,
          "Série removida",
        );
      }
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
