import { Component, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth.service';

@Component({ standalone: true, imports: [ReactiveFormsModule, RouterLink], templateUrl: './register.component.html', styleUrl: '../auth.scss' })
export class RegisterComponent {
  readonly loading = signal(false);
  readonly error = signal('');
  readonly form;
  constructor(private fb: FormBuilder, private auth: AuthService, private router: Router) {
    this.form = this.fb.nonNullable.group({
      name: ['', [Validators.required, Validators.maxLength(120)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]]
    });
  }
  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const value = this.form.getRawValue(); this.loading.set(true); this.error.set('');
    this.auth.register(value.name, value.email, value.password).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: () => void this.router.navigate(['/explorar']),
      error: err => this.error.set(err.status === 409 ? 'Este e-mail já está cadastrado.' : 'Não foi possível criar sua conta.')
    });
  }
}
