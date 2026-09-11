export interface User { id: string; name: string; email: string; }
export interface AuthResponse { token: string; expiresAtUtc: string; user: User; }
