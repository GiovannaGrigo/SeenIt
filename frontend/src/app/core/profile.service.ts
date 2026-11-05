import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";

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

  private readonly apiUrl = "https://localhost:7042/api/profile";

  getProfile(): Observable<ProfileResponse> {
    return this.http.get<ProfileResponse>(this.apiUrl);
  }

  updateName(name: string): Observable<ProfileResponse> {
    return this.http.patch<ProfileResponse>(this.apiUrl, { name });
  }

  getStats(): Observable<{ series: number; episodes: number; favorites: number }> {
    return this.http.get<{ series: number; episodes: number; favorites: number }>(`${this.apiUrl}/stats`);
  }

  getWatchTime(timeZone: string): Observable<WatchTimeResponse> {
    return this.http.get<WatchTimeResponse>(`${this.apiUrl}/watch-time`, { params: { timeZone } });
  }

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/password`, { currentPassword, newPassword });
  }

  deleteAccount(password: string) {
    return this.http.delete<void>(this.apiUrl, {
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
