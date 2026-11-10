import {
  Component,
  DestroyRef,
  inject,
  input,
  OnInit,
  output,
  signal,
} from "@angular/core";
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from "@angular/forms";
import { catchError, concatMap, defer, finalize, forkJoin, of, take, tap } from "rxjs";
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import {
  EpisodioAssistidoResponse,
  PersonagemEpisodio,
  SentimentoEpisodio,
  VotacaoPersonagemResponse,
} from "../../models/episodio-assistido.model";
import { EpisodioAssistidoService } from "../../core/episodio-assistido.service";
import { Episode } from "../../models/series.models";
import { NotificationService } from "../../core/notification.service";
import { httpErrorMessage } from '../../core/http-error';

@Component({
  selector: "app-episodio-modal",
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: "./episodio-modal.component.html",
  styleUrl: "./episodio-modal.component.scss",
})
export class EpisodioModalComponent implements OnInit {
  private readonly service = inject(EpisodioAssistidoService);
  private readonly notification = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);
  private savedSelection: string | null = null;

  readonly episodio = input.required<Episode>();
  readonly seriesId = input.required<number>();

  readonly registroAtual = input<EpisodioAssistidoResponse | null>(null);

  readonly fechar = output<void>();

  readonly salvo = output<EpisodioAssistidoResponse>();

  readonly personagens = signal<PersonagemEpisodio[]>([]);
  readonly votacao = signal<VotacaoPersonagemResponse[]>([]);

  readonly carregando = signal(true);
  readonly salvando = signal(false);
  readonly mensagemErro = signal<string | null>(null);
  readonly salvoComSucesso = signal(false);
  readonly carregandoVotacao = signal(false);
  readonly erroVotacao = signal<string | null>(null);

  readonly sentimentos = [
    {
      valor: SentimentoEpisodio.Feliz,
      descricao: "Feliz",
      emoji: "😊",
    },
    {
      valor: SentimentoEpisodio.Triste,
      descricao: "Triste",
      emoji: "😢",
    },
    {
      valor: SentimentoEpisodio.Raiva,
      descricao: "Raiva",
      emoji: "😡",
    },
    {
      valor: SentimentoEpisodio.Comovido,
      descricao: "Comovido",
      emoji: "🥹",
    },
    {
      valor: SentimentoEpisodio.Engracado,
      descricao: "Engraçado",
      emoji: "😂",
    },
  ];

  readonly form = new FormGroup({
    sentimentos: new FormControl<SentimentoEpisodio[]>([], {
      nonNullable: true,
      validators: [Validators.required],
    }),

    externalCharacterId: new FormControl<number | null>(null, [
      Validators.required,
      Validators.min(1),
    ]),
  });

  ngOnInit(): void {
    this.preencherRegistroExistente();
    this.carregarDados();
  }

  alternarSentimento(sentimento: SentimentoEpisodio): void {
    if (this.salvando()) return;
    const sentimentosAtuais = this.form.controls.sentimentos.value;

    const jaSelecionado = sentimentosAtuais.includes(sentimento);

    const novosSentimentos = jaSelecionado
      ? sentimentosAtuais.filter((item) => item !== sentimento)
      : [...sentimentosAtuais, sentimento];

    this.form.controls.sentimentos.setValue(novosSentimentos);

    this.form.controls.sentimentos.markAsTouched();
    this.form.controls.sentimentos.markAsDirty();
  }

  sentimentoSelecionado(sentimento: SentimentoEpisodio): boolean {
    return this.form.controls.sentimentos.value.includes(sentimento);
  }

  selecionarPersonagem(personagemId: number): void {
    if (this.salvando()) return;
    this.form.controls.externalCharacterId.setValue(personagemId);
  }

  personagemSelecionado(personagemId: number): boolean {
    return this.form.controls.externalCharacterId.value === personagemId;
  }

  salvar(): void {
    if (this.carregando() || this.salvando() || this.carregandoVotacao() || this.destroyRef.destroyed) return;
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      this.mensagemErro.set("Selecione pelo menos uma emoção e um personagem.");
      return;
    }
    const selection = this.selectionKey();
    if (selection === this.savedSelection) return;

    const sentimentos = this.form.controls.sentimentos.value;

    const personagemId = this.form.controls.externalCharacterId.value;

    const personagem = this.personagens().find(
      (item) => item.externalCharacterId === personagemId,
    );

    if (sentimentos.length === 0 || !personagem) {
      this.mensagemErro.set("Selecione pelo menos uma emoção e um personagem.");

      return;
    }

    const episodio = this.episodio();

    this.salvando.set(true);
    this.salvoComSucesso.set(false);
    this.mensagemErro.set(null);

    this.service
      .marcarComoAssistido(episodio.id, {
        externalSeriesId: this.seriesId(),
        temporada: episodio.season,
        numeroEpisodio: episodio.number,
        nomeEpisodio: episodio.name,
        sentimentos,
        externalCharacterId: personagem.externalCharacterId,
        personagemFavoritoNome: personagem.nome,
      }, true)
      .pipe(
        take(1),
        tap(registro => {
          // A confirmed write is acknowledged before any follow-up read.
          this.savedSelection = selection;
          this.salvoComSucesso.set(true);
          this.salvo.emit(registro);
          this.notification.success(`${episodio.name} foi marcado como assistido.`, "Episódio assistido");
        }),
        concatMap(() => { this.salvando.set(false); return this.consultarVotacao(); }),
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.salvando.set(false)),
      )
      .subscribe({
        // consultarVotacao catches its own HTTP errors; only write failures reach here.
        error: error => this.mensagemErro.set(httpErrorMessage(error)),
      });
  }

  podeSalvar(): boolean {
    return this.form.valid && !this.carregando() && !this.salvando() && !this.carregandoVotacao()
      && this.selectionKey() !== this.savedSelection;
  }

  solicitarFechamento(): void {
    if (!this.salvando()) this.fechar.emit();
  }

  repetirVotacao(): void {
    if (!this.erroVotacao() || this.carregando() || this.salvando() || this.carregandoVotacao() || this.destroyRef.destroyed) return;
    this.consultarVotacao().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  private selectionKey(): string {
    return JSON.stringify([this.form.controls.externalCharacterId.value, [...this.form.controls.sentimentos.value].sort()]);
  }

  private consultarVotacao() {
    return defer(() => {
      this.carregandoVotacao.set(true);
      this.erroVotacao.set(null);
      return this.service.obterVotacao(this.episodio().id, true);
    }).pipe(
      take(1),
      tap(votacao => this.votacao.set(votacao)),
      catchError(error => { this.erroVotacao.set(httpErrorMessage(error)); return of(null); }),
      finalize(() => this.carregandoVotacao.set(false)),
    );
  }

  private carregarDados(): void {
    forkJoin({
      personagens: this.service.obterPersonagens(
        this.seriesId(),
        this.episodio().id,
        true,
      ),

      votacao: this.consultarVotacao(),
    })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.carregando.set(false)))
      .subscribe({
        next: (resultado) => {
          this.personagens.set(resultado.personagens);
        },
        error: error => this.mensagemErro.set(httpErrorMessage(error)),
      });
  }

  private preencherRegistroExistente(): void {
    const registro = this.registroAtual();

    if (!registro) {
      return;
    }

    this.form.patchValue({
      sentimentos: registro.sentimentos ?? [],
      externalCharacterId: registro.externalCharacterId,
    });
  }
}
