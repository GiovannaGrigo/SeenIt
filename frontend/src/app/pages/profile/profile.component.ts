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
import { NotificationService } from "../../core/notification.service";

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
  private readonly notification = inject(NotificationService);
  readonly profile = signal<ProfileResponse | null>(null);
  readonly uploadingAvatar = signal(false);
  readonly savingProfile = signal(false);
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

          this.notification.success(
            "Sua foto de perfil foi atualizada.",
            "Foto atualizada",
          );
        },
      });
  }

  saveProfile(): void {
    this.form.markAllAsTouched();

    if (this.form.invalid || this.savingProfile()) {
      return;
    }

    this.savingProfile.set(true);

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

          this.notification.success(
            "Suas informações foram atualizadas.",
            "Perfil atualizado",
          );
        },
      });
  }

  deleteAvatar(): void {
    this.uploadingAvatar.set(true);

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

          this.notification.success(
            "Sua foto de perfil foi removida.",
            "Foto removida",
          );
        }
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
      }
    });
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
          this.notification.success(
            "Sua conta foi excluída com sucesso.",
            "Conta excluída",
          );
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
