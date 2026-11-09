import { HttpInterceptorFn } from '@angular/common/http';
import { DOCUMENT } from '@angular/common';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const token = inject(AuthService).token;
  const baseUri = inject(DOCUMENT).baseURI;
  let isApiRequest = false;

  try {
    const apiUrl = new URL(environment.apiUrl);
    const requestUrl = new URL(request.url, baseUri);
    const apiPath = apiUrl.pathname.replace(/\/+$/, '');
    isApiRequest = requestUrl.origin === apiUrl.origin &&
      (requestUrl.pathname === apiPath || requestUrl.pathname.startsWith(`${apiPath}/`));
  } catch {
    // URLs inválidas seguem sem receber o token da sessão.
  }

  return next(token && isApiRequest
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request);
};
