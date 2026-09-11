export enum SeriesStatus {
  InList = 1,
  Watching = 2,
  Finished = 3
}

export interface SeriesSummary {
  id: number;
  name: string;
  posterUrl?: string;
  summary?: string;
  premiered?: string;
  ended?: string;
  rating?: number;
  genres: string[];
}

export interface SeriesDetails extends SeriesSummary {
  originalPosterUrl?: string;
  network?: string;
  status?: string;
}

export interface Episode {
  id: number;
  season: number;
  number: number;
  name: string;
  summary?: string;
  airDate?: string;
  runtime?: number;
  imageUrl?: string;
}

export interface UserSeries {
  id: string;
  externalSeriesId: number;
  name: string;
  posterUrl?: string;
  summary?: string;
  status: SeriesStatus;
  addedAtUtc: string;
  updatedAtUtc: string;
}
