import { Component, ElementRef, OnInit, ViewChild, computed, inject, signal } from "@angular/core";
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
import { WatchTimeComponent } from "./watch-time.component";
import { httpErrorMessage } from '../../core/http-error';

@Component({
  selector: "app-profile",
  standalone: true,
  imports: [ReactiveFormsModule, WatchTimeComponent],
  templateUrl: "./profile.component.html",
  styleUrl: "./profile.component.scss",
})
export class ProfileComponent implements OnInit {
  private readonly profileService = inject(ProfileService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notification = inject(NotificationService);
  readonly profile = signal<ProfileResponse | null>(null);
  readonly loadingProfile = signal(false);
  readonly loadingStats = signal(false);
  readonly profileError = signal<string | null>(null);
  readonly statsError = signal<string | null>(null);
  readonly uploadingAvatar = signal(false);
  readonly savingProfile = signal(false);
  readonly deleteModalOpen = signal(false);
  readonly deletingAccount = signal(false);
  readonly deleteAccountError = signal<string | null>(null);
  @ViewChild('passwordDialog') private passwordDialog!: ElementRef<HTMLDialogElement>;
  readonly changingPassword = signal(false);
  readonly passwordError = signal<string | null>(null);
  readonly visiblePasswords = signal<Record<string, boolean>>({});
  readonly stats = signal<{ series: number; episodes: number; favorites: number } | null>(null);
  readonly initials = computed(() => {
    const names = this.profile()?.name.trim().split(/\s+/) ?? [];
    return (names.length > 1 ? names[0][0] + names[names.length - 1][0] : names[0]?.[0] ?? 'U').toUpperCase();
  });
  readonly passwordFields = [
    { key: 'currentPassword', label: 'Senha atual', placeholder: 'Digite sua senha atual', autocomplete: 'current-password' },
    { key: 'newPassword', label: 'Nova senha', placeholder: 'Mínimo de 8 caracteres', autocomplete: 'new-password' },
    { key: 'confirmPassword', label: 'Confirmar nova senha', placeholder: 'Repita a nova senha', autocomplete: 'new-password' },
  ] as const;
  readonly passwordForm = new FormGroup({
    currentPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    newPassword: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8), Validators.maxLength(128)] }),
    confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  }, { validators: (group) => group.get('newPassword')?.value === group.get('confirmPassword')?.value ? null : { mismatch: true } });

  discardChanges(): void {
    this.form.reset({ name: this.profile()?.name ?? '' });
  }

  openPasswordModal(): void {
    this.passwordForm.reset();
    this.passwordError.set(null);
    this.visiblePasswords.set({});
    this.passwordDialog.nativeElement.showModal();
  }

  closePasswordModal(): void {
    if (this.changingPassword()) return;
    this.passwordDialog.nativeElement.close();
    this.passwordForm.reset();
    this.passwordError.set(null);
    this.visiblePasswords.set({});
  }

  togglePassword(key: string): void {
    this.visiblePasswords.update(value => ({ ...value, [key]: !value[key] }));
  }

  changePassword(): void {
    this.passwordForm.markAllAsTouched();
    if (this.passwordForm.invalid || this.changingPassword()) return;
    const { currentPassword, newPassword } = this.passwordForm.getRawValue();
    this.changingPassword.set(true);
    this.passwordError.set(null);
    this.profileService.changePassword(currentPassword, newPassword)
      .pipe(finalize(() => this.changingPassword.set(false)))
      .subscribe({
        next: () => {
          this.changingPassword.set(false);
          this.closePasswordModal();
          this.notification.success('Sua senha foi atualizada.', 'Senha atualizada');
        },
        error: error => this.passwordError.set(httpErrorMessage(error)),
      });
  }

  readonly deletePassword = new FormControl("", {
    nonNullable: true,
    validators: [Validators.required],
  });

  readonly form = new FormGroup({
    name: new FormControl("", {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(120)],
    }),
  });

  ngOnInit(): void {
    this.loadProfile();
    this.loadStats();
  }

  loadStats(): void {
    if (this.loadingStats()) return;
    this.loadingStats.set(true);
    this.statsError.set(null);
    this.profileService.getStats(true).pipe(finalize(() => this.loadingStats.set(false))).subscribe({
      next: stats => this.stats.set(stats),
      error: error => this.statsError.set(httpErrorMessage(error)),
    });
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
        error: () => {}, // The interceptor owns this error.
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
        error: () => {},
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
        },
        error: () => {},
      });
  }

  loadProfile(): void {
    if (this.loadingProfile()) return;
    this.loadingProfile.set(true);
    this.profileError.set(null);
    this.profileService.getProfile(true).pipe(finalize(() => this.loadingProfile.set(false))).subscribe({
      next: (profile) => {
        this.profile.set(profile);

        this.form.controls.name.setValue(profile.name);

        this.auth.updateUser({
          name: profile.name,
          avatarUrl: profile.avatarUrl,
        });
      },
      error: error => this.profileError.set(httpErrorMessage(error)),
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
            httpErrorMessage(error),
          );
        },
      });
  }
}
