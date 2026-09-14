export interface User {
  id: string;
  name: string;
  email: string;
  avatarUrl: string | null;
}

export interface AuthResponse {
  token: string;
  expiresAtUtc: string;
  user: User;
}
