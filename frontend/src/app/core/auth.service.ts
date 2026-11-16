import { Injectable, signal, untracked } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { tap } from "rxjs";
import { AuthResponse, User } from "../models/auth.models";
import { environment } from "../../environments/environment";
import { localErrorContext } from './http-error';

@Injectable({ providedIn: "root" })
export class AuthService {
  private readonly apiUrl = `${environment.apiUrl}/auth`;

  private readonly tokenKey = "seenit_token";
  private readonly userKey = "seenit_user";

  private readonly tokenSignal = signal<string | null>(
    localStorage.getItem(this.tokenKey),
  );

  private readonly userSignal = signal<User | null>(this.readUser());

  readonly user = this.userSignal.asReadonly();

  readonly isAuthenticated = (): boolean => {
    const token = this.tokenSignal();
    const user = this.userSignal();
    if (token && user && this.hasValidLifetime(token)) {
      return true;
    }

    if (token !== null || user !== null) {
      untracked(() => this.logout());
    }
    return false;
  };

  constructor(private readonly http: HttpClient) {
    if (!this.isAuthenticated()) this.logout();
  }

  get token(): string | null {
    return this.tokenSignal();
  }

  login(email: string, password: string) {
    return this.http
      .post<AuthResponse>(`${this.apiUrl}/login`, {
        email,
        password,
      }, { context: localErrorContext() })
      .pipe(tap((response) => this.saveSession(response)));
  }

  register(name: string, email: string, password: string) {
    return this.http
      .post<AuthResponse>(`${this.apiUrl}/register`, {
        name,
        email,
        password,
      }, { context: localErrorContext() })
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
      const user: unknown = raw ? JSON.parse(raw) : null;
      if (!user || typeof user !== "object") return null;
      const candidate = user as Record<string, unknown>;
      return typeof candidate['id'] === "string" && !!candidate['id'] &&
        typeof candidate['name'] === "string" &&
        typeof candidate['email'] === "string" &&
        (candidate['avatarUrl'] === null || typeof candidate['avatarUrl'] === "string")
        ? user as User : null;
    } catch {
      return null;
    }
  }

  private hasValidLifetime(token: string): boolean {
    try {
      const parts = token.split('.');
      if (parts.length !== 3) return false;
      const bytes = parts.map(part => {
        if (!/^[A-Za-z0-9_-]+$/.test(part) || part.length % 4 === 1) {
          throw new Error('Invalid Base64URL');
        }
        const base64 = part.replace(/-/g, '+').replace(/_/g, '/');
        return Uint8Array.from(atob(base64.padEnd(Math.ceil(base64.length / 4) * 4, '=')), c => c.charCodeAt(0));
      });
      const decoder = new TextDecoder('utf-8', { fatal: true });
      const header = JSON.parse(decoder.decode(bytes[0]));
      const payload = JSON.parse(decoder.decode(bytes[1]));

      return !!header && typeof header === 'object' && !Array.isArray(header) &&
        typeof header.alg === 'string' && !!header.alg && header.alg !== 'none' &&
        !!payload && typeof payload === 'object' && !Array.isArray(payload) &&
        typeof payload.exp === 'number' && Number.isFinite(payload.exp) &&
        payload.exp > Date.now() / 1000;
    } catch {
      return false;
    }
  }
}
