export enum SentimentoEpisodio {
  Feliz = "Feliz",
  Triste = "Triste",
  Raiva = "Raiva",
  Comovido = "Comovido",
  Engracado = "Engracado",
}

export interface PersonagemEpisodio {
  externalCharacterId: number;
  nome: string;
  imagemUrl?: string;
  atorNome?: string;
}

export interface MarcarEpisodioAssistidoRequest {
  externalSeriesId: number;
  temporada: number;
  numeroEpisodio: number;
  nomeEpisodio: string;
  sentimentos: SentimentoEpisodio[];
  externalCharacterId: number;
  personagemFavoritoNome: string;
}

export interface EpisodioAssistidoResponse {
  externalEpisodeId: number;
  externalSeriesId: number;
  temporada: number;
  numeroEpisodio: number;
  nomeEpisodio: string;
  sentimentos: SentimentoEpisodio[];
  externalCharacterId: number;
  personagemFavoritoNome: string;
  assistidoEmUtc: string;
}

export interface VotacaoPersonagemResponse {
  externalCharacterId: number;
  personagemNome: string;
  totalVotos: number;
  porcentagem: number;
}
