import { HttpClient } from "@angular/common/http";
import { inject, Injectable } from "@angular/core";
import { Observable } from "rxjs";

import { environment } from "../../environments/environment";
import { localErrorContext } from './http-error';
import {
  EpisodioAssistidoResponse,
  MarcarEpisodioAssistidoRequest,
  PersonagemEpisodio,
  VotacaoPersonagemResponse,
} from "../models/episodio-assistido.model";

@Injectable({
  providedIn: "root",
})
export class EpisodioAssistidoService {
  private readonly http = inject(HttpClient);

  private readonly apiUrl = environment.apiUrl;

  obterPersonagens(
    seriesId: number,
    episodeId: number,
    local = false,
  ): Observable<PersonagemEpisodio[]> {
    return this.http.get<PersonagemEpisodio[]>(
      `${this.apiUrl}/series/${seriesId}` +
        `/episodios/${episodeId}/personagens`,
      { context: local ? localErrorContext() : undefined },
    );
  }

  obterAssistidosPorSerie(
    seriesId: number,
    local = false,
  ): Observable<EpisodioAssistidoResponse[]> {
    return this.http.get<EpisodioAssistidoResponse[]>(
      `${this.apiUrl}/episodios/series/${seriesId}`,
      { context: local ? localErrorContext() : undefined },
    );
  }

  obterVotacao(episodeId: number, local = false): Observable<VotacaoPersonagemResponse[]> {
    return this.http.get<VotacaoPersonagemResponse[]>(
      `${this.apiUrl}/episodios/${episodeId}/votacao-personagens`,
      { context: local ? localErrorContext() : undefined },
    );
  }

  marcarComoAssistido(
    episodeId: number,
    request: MarcarEpisodioAssistidoRequest,
    local = false,
  ): Observable<EpisodioAssistidoResponse> {
    return this.http.put<EpisodioAssistidoResponse>(
      `${this.apiUrl}/episodios/${episodeId}/assistido`,
      request,
      { context: local ? localErrorContext() : undefined },
    );
  }

  marcarTemporadaComoAssistida(
    seriesId: number,
    temporada: number,
  ): Observable<EpisodioAssistidoResponse[]> {
    return this.http.put<EpisodioAssistidoResponse[]>(
      `${this.apiUrl}/episodios/series/${seriesId}/temporadas/${temporada}/assistida`,
      {},
    );
  }

  desmarcarTemporada(
    seriesId: number,
    temporada: number,
  ): Observable<EpisodioAssistidoResponse[]> {
    return this.http.delete<EpisodioAssistidoResponse[]>(
      `${this.apiUrl}/episodios/series/${seriesId}/temporadas/${temporada}/assistida`,
    );
  }
}
