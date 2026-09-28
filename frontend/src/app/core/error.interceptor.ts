import { HttpErrorResponse, HttpInterceptorFn } from "@angular/common/http";

import { inject } from "@angular/core";
import { catchError, throwError } from "rxjs";
import { NotificationService } from "./notification.service";

export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const notification = inject(NotificationService);

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      notification.error(getErrorMessage(error));

      return throwError(() => error);
    }),
  );
};

function getErrorMessage(error: HttpErrorResponse): string {
  if (error.status === 0) {
    return "Não foi possível conectar ao servidor.";
  }

  if (typeof error.error === "string") {
    return error.error;
  }

  if (error.error?.message) {
    return error.error.message;
  }

  if (error.error?.detail) {
    return error.error.detail;
  }

  if (error.status === 401) {
    return "Sua sessão expirou ou você não está autorizado.";
  }

  if (error.status === 403) {
    return "Você não tem permissão para realizar esta ação.";
  }

  if (error.status === 404) {
    return "O recurso solicitado não foi encontrado.";
  }

  if (error.status >= 500) {
    return "Ocorreu um erro no servidor. Tente novamente.";
  }

  return "Não foi possível concluir a operação.";
}
