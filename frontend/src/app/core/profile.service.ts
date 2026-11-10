import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../environments/environment";
import { localErrorContext } from './http-error';

export interface ProfileResponse {
  id: string;
  name: string;
  email: string;
  avatarUrl: string | null;
}

export interface WatchTimeResponse {
  totalMinutes: number;
  episodesWithoutRuntime: number;
  today: string;
  days: { date: string; minutes: number }[];
}

@Injectable({
  providedIn: "root",
})
export class ProfileService {
  private readonly http = inject(HttpClient);

  private readonly apiUrl = `${environment.apiUrl}/profile`;

  getProfile(local = false): Observable<ProfileResponse> {
    return this.http.get<ProfileResponse>(this.apiUrl, { context: local ? localErrorContext() : undefined });
  }

  updateName(name: string): Observable<ProfileResponse> {
    return this.http.patch<ProfileResponse>(this.apiUrl, { name });
  }

  getStats(local = false): Observable<{ series: number; episodes: number; favorites: number }> {
    return this.http.get<{ series: number; episodes: number; favorites: number }>(`${this.apiUrl}/stats`, { context: local ? localErrorContext() : undefined });
  }

  getWatchTime(timeZone: string): Observable<WatchTimeResponse> {
    return this.http.get<WatchTimeResponse>(`${this.apiUrl}/watch-time`, { params: { timeZone }, context: localErrorContext() });
  }

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/password`, { currentPassword, newPassword }, { context: localErrorContext() });
  }

  deleteAccount(password: string) {
    return this.http.delete<void>(this.apiUrl, {
      context: localErrorContext(),
      body: {
        password,
      },
    });
  }

  updateAvatar(file: File): Observable<ProfileResponse> {
    const formData = new FormData();

    formData.append("avatar", file);

    return this.http.put<ProfileResponse>(`${this.apiUrl}/avatar`, formData);
  }

  deleteAvatar(): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/avatar`);
  }
}
