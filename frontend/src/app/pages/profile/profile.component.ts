import { Component, OnInit, inject, signal } from "@angular/core";
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from "@angular/forms";

import { finalize } from "rxjs";

import { AuthService } from "../../core/auth.service";
import { ProfileResponse, ProfileService } from "../../core/profile.service";
import { Router } from "@angular/router";

@Component({
  selector: "app-profile",
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: "./profile.component.html",
  styleUrl: "./profile.component.scss",
})
export class ProfileComponent implements OnInit {
  private readonly profileService = inject(ProfileService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly profile = signal<ProfileResponse | null>(null);
  readonly uploadingAvatar = signal(false);
  readonly savingProfile = signal(false);
  readonly successMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly deleteModalOpen = signal(false);
  readonly deletingAccount = signal(false);
  readonly deleteAccountError = signal<string | null>(null);

  readonly deletePassword = new FormControl("", {
    nonNullable: true,
    validators: [Validators.required],
  });

  readonly form = new FormGroup({
    name: new FormControl("", {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
  });

  ngOnInit(): void {
    this.loadProfile();
  }

  selectAvatar(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    if (!file) {
      return;
    }

    this.uploadingAvatar.set(true);
    this.clearMessages();

    this.profileService
      .updateAvatar(file)
      .pipe(
        finalize(() => {
          this.uploadingAvatar.set(false);
          input.value = "";
        }),
      )
      .subscribe({
        next: (profile) => {
          this.profile.set(profile);

          this.auth.updateUser({
            avatarUrl: profile.avatarUrl,
          });

          this.successMessage.set("Foto atualizada com sucesso.");
        },

        error: () => {
          this.errorMessage.set("Não foi possível atualizar a foto.");
        },
      });
  }

  saveProfile(): void {
    this.form.markAllAsTouched();

    if (this.form.invalid || this.savingProfile()) {
      return;
    }

    this.savingProfile.set(true);
    this.clearMessages();

    this.profileService
      .updateName(this.form.controls.name.value)
      .pipe(
        finalize(() => {
          this.savingProfile.set(false);
        }),
      )
      .subscribe({
        next: (profile) => {
          this.profile.set(profile);

          this.auth.updateUser({
            name: profile.name,
          });

          this.form.markAsPristine();

          this.successMessage.set("Perfil atualizado com sucesso.");
        },

        error: () => {
          this.errorMessage.set("Não foi possível atualizar o perfil.");
        },
      });
  }

  deleteAvatar(): void {
    this.uploadingAvatar.set(true);
    this.clearMessages();

    this.profileService
      .deleteAvatar()
      .pipe(
        finalize(() => {
          this.uploadingAvatar.set(false);
        }),
      )
      .subscribe({
        next: () => {
          this.profile.update((profile) =>
            profile
              ? {
                  ...profile,
                  avatarUrl: null,
                }
              : null,
          );

          this.auth.updateUser({
            avatarUrl: null,
          });

          this.successMessage.set("Foto removida com sucesso.");
        },

        error: () => {
          this.errorMessage.set("Não foi possível remover a foto.");
        },
      });
  }

  private loadProfile(): void {
    this.profileService.getProfile().subscribe({
      next: (profile) => {
        this.profile.set(profile);

        this.form.controls.name.setValue(profile.name);

        this.auth.updateUser({
          name: profile.name,
          avatarUrl: profile.avatarUrl,
        });
      },

      error: () => {
        this.errorMessage.set("Não foi possível carregar o perfil.");
      },
    });
  }

  private clearMessages(): void {
    this.successMessage.set(null);
    this.errorMessage.set(null);
  }

  openDeleteModal(): void {
    this.deletePassword.reset();
    this.deleteAccountError.set(null);
    this.deleteModalOpen.set(true);
  }

  closeDeleteModal(): void {
    if (this.deletingAccount()) {
      return;
    }

    this.deleteModalOpen.set(false);
    this.deleteAccountError.set(null);
  }

  deleteAccount(): void {
    console.log("Tentativa de excluir conta:", {
      senhaInformada: this.deletePassword.value.length > 0,
      formularioValido: this.deletePassword.valid,
      excluindo: this.deletingAccount(),
    });
    this.deletePassword.markAsTouched();

    if (this.deletePassword.invalid || this.deletingAccount()) {
      return;
    }

    this.deletingAccount.set(true);
    this.deleteAccountError.set(null);

    this.profileService
      .deleteAccount(this.deletePassword.value)
      .pipe(finalize(() => this.deletingAccount.set(false)))
      .subscribe({
        next: () => {
          this.auth.logout();
          this.router.navigateByUrl("/login");
        },
        error: (error) => {
          this.deleteAccountError.set(
            error.error?.message ??
              "Não foi possível excluir sua conta. Tente novamente.",
          );
        },
      });
  }
}
