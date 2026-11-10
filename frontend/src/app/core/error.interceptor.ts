import { HttpErrorResponse, HttpInterceptorFn } from "@angular/common/http";

import { inject } from "@angular/core";
import { catchError, throwError } from "rxjs";
import { NotificationService } from "./notification.service";
import { HANDLE_ERROR_LOCALLY, httpErrorMessage } from './http-error';

export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const notification = inject(NotificationService);

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      if (!request.context.get(HANDLE_ERROR_LOCALLY)) notification.error(httpErrorMessage(error));

      return throwError(() => error);
    }),
  );
};
