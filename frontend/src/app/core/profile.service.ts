import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";

export interface ProfileResponse {
  id: string;
  name: string;
  email: string;
  avatarUrl: string | null;
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

  updateAvatar(file: File): Observable<ProfileResponse> {
    const formData = new FormData();

    formData.append("avatar", file);

    return this.http.put<ProfileResponse>(`${this.apiUrl}/avatar`, formData);
  }

  deleteAvatar(): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/avatar`);
  }
}
