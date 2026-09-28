import { bootstrapApplication } from "@angular/platform-browser";
import { provideHttpClient, withInterceptors } from "@angular/common/http";
import { provideRouter } from "@angular/router";
import { provideAnimationsAsync } from "@angular/platform-browser/animations/async";

import { providePrimeNG } from "primeng/config";
import { MessageService } from "primeng/api";
import Aura from "@primeuix/themes/aura";

import { AppComponent } from "./app/app.component";
import { routes } from "./app/app.routes";
import { authInterceptor } from "./app/core/auth.interceptor";
import { errorInterceptor } from "./app/core/error.interceptor";

bootstrapApplication(AppComponent, {
  providers: [
    provideRouter(routes),

    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),

    provideAnimationsAsync(),

    providePrimeNG({
      theme: {
        preset: Aura,
        options: {
          darkModeSelector: ".dark-theme",
        },
      },
    }),

    MessageService,
  ],
}).catch(console.error);
