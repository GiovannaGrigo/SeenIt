import { Routes } from "@angular/router";
import { authGuard } from "./core/auth.guard";

export const routes: Routes = [
  {
    path: "login",
    loadComponent: () =>
      import("./pages/login/login.component").then((m) => m.LoginComponent),
  },
  {
    path: "cadastro",
    loadComponent: () =>
      import("./pages/register/register.component").then(
        (m) => m.RegisterComponent,
      ),
  },
  {
    path: "perfil",
    loadComponent: () =>
      import("./pages/profile/profile.component").then(
        (component) => component.ProfileComponent,
      ),
  },
  {
    path: "minha-lista",
    canActivate: [authGuard],
    loadComponent: () =>
      import("./pages/library/library.component").then(
        (m) => m.LibraryComponent,
      ),
  },
  {
    path: "explorar",
    canActivate: [authGuard],
    loadComponent: () =>
      import("./pages/explore/explore.component").then(
        (m) => m.ExploreComponent,
      ),
  },
  {
    path: "serie/:id",
    canActivate: [authGuard],
    loadComponent: () =>
      import("./pages/series-detail/series-detail.component").then(
        (m) => m.SeriesDetailComponent,
      ),
  },
  { path: "", pathMatch: "full", redirectTo: "minha-lista" },
  { path: "**", redirectTo: "minha-lista" },
];
