import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Episode, SeriesDetails, SeriesStatus, SeriesSummary, UserSeries } from '../models/series.models';

@Injectable({ providedIn: 'root' })
export class SeriesService {
  private readonly apiUrl = 'https://localhost:7042/api';
  constructor(private readonly http: HttpClient) {}

  search(query: string) {
    return this.http.get<SeriesSummary[]>(`${this.apiUrl}/series/search`, {
      params: new HttpParams().set('q', query)
    });
  }

  getDetails(id: number) { return this.http.get<SeriesDetails>(`${this.apiUrl}/series/${id}`); }
  getEpisodes(id: number) { return this.http.get<Episode[]>(`${this.apiUrl}/series/${id}/episodes`); }
  getMySeries(status?: SeriesStatus) {
    const params = status ? new HttpParams().set('status', status) : undefined;
    return this.http.get<UserSeries[]>(`${this.apiUrl}/my-series`, { params });
  }

  add(series: SeriesSummary | SeriesDetails, status: SeriesStatus) {
    return this.http.post<UserSeries>(`${this.apiUrl}/my-series`, {
      externalSeriesId: series.id,
      name: series.name,
      posterUrl: series.posterUrl,
      summary: series.summary,
      status
    });
  }

  updateStatus(externalSeriesId: number, status: SeriesStatus) {
    return this.http.put<UserSeries>(`${this.apiUrl}/my-series/${externalSeriesId}/status`, { status });
  }

  remove(externalSeriesId: number) {
    return this.http.delete<void>(`${this.apiUrl}/my-series/${externalSeriesId}`);
  }
}
