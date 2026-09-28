import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';
import { ThemeService } from './core/theme.service';
import { NotificationService } from './core/notification.service';
import { ToastModule } from 'primeng/toast';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  readonly auth = inject(AuthService);
  readonly theme = inject(ThemeService);
  private readonly notification = inject(NotificationService);
  private readonly router = inject(Router);

  logout(): void {
    this.auth.logout();
    this.notification.success('Você saiu da sua conta.', 'Sessão encerrada');
    void this.router.navigate(['/login']);
  }

  toggleTheme(): void {
    this.theme.toggle();
  }
}
