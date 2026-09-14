import { Injectable, computed, signal } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { tap } from "rxjs";
import { AuthResponse, User } from "../models/auth.models";

@Injectable({ providedIn: "root" })
export class AuthService {
  private readonly apiUrl = "https://localhost:7042/api/auth";

  private readonly tokenKey = "seenit_token";
  private readonly userKey = "seenit_user";

  private readonly tokenSignal = signal<string | null>(
    localStorage.getItem(this.tokenKey),
  );

  private readonly userSignal = signal<User | null>(this.readUser());

  readonly user = this.userSignal.asReadonly();

  readonly isAuthenticated = computed(() => {
    return !!this.tokenSignal() && !!this.userSignal();
  });

  constructor(private readonly http: HttpClient) {}

  get token(): string | null {
    return this.tokenSignal();
  }

  login(email: string, password: string) {
    return this.http
      .post<AuthResponse>(`${this.apiUrl}/login`, {
        email,
        password,
      })
      .pipe(tap((response) => this.saveSession(response)));
  }

  register(name: string, email: string, password: string) {
    return this.http
      .post<AuthResponse>(`${this.apiUrl}/register`, {
        name,
        email,
        password,
      })
      .pipe(tap((response) => this.saveSession(response)));
  }

  updateUser(changes: Partial<User>): void {
    const currentUser = this.userSignal();

    if (!currentUser) {
      return;
    }

    const updatedUser: User = {
      ...currentUser,
      ...changes,
    };

    localStorage.setItem(this.userKey, JSON.stringify(updatedUser));

    this.userSignal.set(updatedUser);
  }

  logout(): void {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);

    this.tokenSignal.set(null);
    this.userSignal.set(null);
  }

  private saveSession(response: AuthResponse): void {
    console.log("Response login:", response);
    console.log("User recebido:", response.user);
    console.log("Avatar recebido:", response.user.avatarUrl);

    localStorage.setItem(this.tokenKey, response.token);

    localStorage.setItem(this.userKey, JSON.stringify(response.user));

    this.tokenSignal.set(response.token);
    this.userSignal.set(response.user);

    console.log("User no signal:", this.userSignal());
  }

  private readUser(): User | null {
    const raw = localStorage.getItem(this.userKey);

    try {
      return raw ? (JSON.parse(raw) as User) : null;
    } catch {
      return null;
    }
  }
}
