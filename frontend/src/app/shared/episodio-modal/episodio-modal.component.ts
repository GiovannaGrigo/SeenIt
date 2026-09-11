import {
  Component,
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
import { finalize, forkJoin, map, switchMap } from "rxjs";

import {
  EpisodioAssistidoResponse,
  PersonagemEpisodio,
  SentimentoEpisodio,
  VotacaoPersonagemResponse,
} from "../../models/episodio-assistido.model";
import { EpisodioAssistidoService } from "src/app/core/episodio-assistido.service";
import { Episode } from "src/app/models/series.models";

@Component({
  selector: "app-episodio-modal",
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: "./episodio-modal.component.html",
  styleUrl: "./episodio-modal.component.scss",
})
export class EpisodioModalComponent implements OnInit {
  private readonly service = inject(EpisodioAssistidoService);

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
    this.form.controls.externalCharacterId.setValue(personagemId);
  }

  personagemSelecionado(personagemId: number): boolean {
    return this.form.controls.externalCharacterId.value === personagemId;
  }

  salvar(): void {
    this.form.markAllAsTouched();

    if (this.form.invalid || this.salvando()) {
      return;
    }

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
      })
      .pipe(
        switchMap((registro) =>
          this.service.obterVotacao(episodio.id).pipe(
            map((votacao) => ({
              registro,
              votacao,
            })),
          ),
        ),
        finalize(() => this.salvando.set(false)),
      )
      .subscribe({
        next: ({ registro, votacao }) => {
          this.votacao.set(votacao);
          this.salvoComSucesso.set(true);

          // Avisa a tela de detalhes para mostrar o badge.
          this.salvo.emit(registro);
        },
        error: () => {
          this.mensagemErro.set("Não foi possível salvar o episódio.");
        },
      });
  }

  private carregarDados(): void {
    forkJoin({
      personagens: this.service.obterPersonagens(
        this.seriesId(),
        this.episodio().id,
      ),

      votacao: this.service.obterVotacao(this.episodio().id),
    })
      .pipe(finalize(() => this.carregando.set(false)))
      .subscribe({
        next: (resultado) => {
          this.personagens.set(resultado.personagens);
          this.votacao.set(resultado.votacao);
        },
        error: () => {
          this.mensagemErro.set("Não foi possível carregar o modal.");
        },
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
