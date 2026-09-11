import { HttpClient } from "@angular/common/http";
import { inject, Injectable } from "@angular/core";
import { Observable } from "rxjs";

import { environment } from "../../environments/environment";
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
  ): Observable<PersonagemEpisodio[]> {
    return this.http.get<PersonagemEpisodio[]>(
      `${this.apiUrl}/series/${seriesId}` +
        `/episodios/${episodeId}/personagens`,
    );
  }

  obterAssistidosPorSerie(
    seriesId: number,
  ): Observable<EpisodioAssistidoResponse[]> {
    return this.http.get<EpisodioAssistidoResponse[]>(
      `${this.apiUrl}/episodios/series/${seriesId}`,
    );
  }

  obterVotacao(episodeId: number): Observable<VotacaoPersonagemResponse[]> {
    return this.http.get<VotacaoPersonagemResponse[]>(
      `${this.apiUrl}/episodios/${episodeId}/votacao-personagens`,
    );
  }

  marcarComoAssistido(
    episodeId: number,
    request: MarcarEpisodioAssistidoRequest,
  ): Observable<EpisodioAssistidoResponse> {
    return this.http.put<EpisodioAssistidoResponse>(
      `${this.apiUrl}/episodios/${episodeId}/assistido`,
      request,
    );
  }
}
