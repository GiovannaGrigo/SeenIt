import { HttpContext, HttpContextToken, HttpErrorResponse } from '@angular/common/http';

// A request has one owner for its error message: its component or the interceptor.
export const HANDLE_ERROR_LOCALLY = new HttpContextToken(() => false);
export const localErrorContext = () => new HttpContext().set(HANDLE_ERROR_LOCALLY, true);

export function retryAfterSeconds(value: string | null, now = Date.now()): number | null {
  if (!value?.trim()) return null;
  const text = value.trim();
  if (/^\d+$/.test(text)) {
    const seconds = Number(text);
    return Number.isSafeInteger(seconds) ? seconds : null;
  }
  // HTTP-date (IMF-fixdate), not arbitrary strings accepted by Date.parse.
  if (!/^[A-Za-z]{3}, \d{2} [A-Za-z]{3} \d{4} \d{2}:\d{2}:\d{2} GMT$/.test(text)) return null;
  const date = Date.parse(text);
  return Number.isFinite(date) && date > now ? Math.ceil((date - now) / 1000) : null;
}

export function httpErrorMessage(error: HttpErrorResponse, login = false): string {
  if (error.status === 0) return 'Não foi possível conectar ao servidor. Verifique sua conexão e tente novamente.';
  if (error.status === 429) {
    const seconds = retryAfterSeconds(error.headers?.get('Retry-After') ?? null);
    return seconds !== null && seconds > 0
      ? `Muitas tentativas. Tente novamente em cerca de ${seconds} segundos.`
      : 'Muitas tentativas. Aguarde um pouco e tente novamente.';
  }
  if (error.status === 401) return login ? 'E-mail ou senha inválidos.' : 'Sua sessão expirou ou você não está autorizado.';
  if (error.status === 403) return 'Você não tem permissão para realizar esta ação.';
  if (error.status === 404) return 'O recurso solicitado não foi encontrado.';
  if (error.status === 503 || error.status === 502 || error.status === 504)
    return 'O serviço está temporariamente indisponível. Tente novamente mais tarde.';
  if (error.status >= 500) return 'Ocorreu um erro no servidor. Tente novamente mais tarde.';
  if (error.status === 400 || error.status === 422) return 'Verifique os dados informados e tente novamente.';
  if (error.status === 409) return 'Não foi possível concluir a alteração. Atualize os dados e tente novamente.';
  return 'Não foi possível concluir a operação. Tente novamente.';
}
