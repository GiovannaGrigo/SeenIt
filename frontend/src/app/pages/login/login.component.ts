import { Component, signal } from "@angular/core";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { Router, RouterLink } from "@angular/router";
import { finalize } from "rxjs";
import { AuthService } from "../../core/auth.service";

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: "./login.component.html",
  styleUrl: "../auth.scss",
})
export class LoginComponent {
  readonly loading = signal(false);
  readonly error = signal("");
  readonly form;

  constructor(
    private fb: FormBuilder,
    private auth: AuthService,
    private router: Router,
  ) {
    this.form = this.fb.nonNullable.group({
      email: ["", [Validators.required, Validators.email]],
      password: ["", Validators.required],
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.error.set("");
    this.auth
      .login(this.form.getRawValue().email, this.form.getRawValue().password)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => {
          this.router
            .navigateByUrl("/minha-lista", { replaceUrl: true })
            .then((navegou) => {
              if (!navegou) {
                this.error.set(
                  "Login realizado, mas não foi possível abrir sua lista.",
                );
                console.error("O Angular cancelou a navegação.");
              }
            })
            .catch((error) => {
              console.error("Erro ao navegar:", error);
              this.error.set(
                "Login realizado, mas ocorreu um erro ao abrir sua lista.",
              );
            });
        },
        error: () => {
          this.error.set("E-mail ou senha inválidos.");
        },
      });
  }
}
