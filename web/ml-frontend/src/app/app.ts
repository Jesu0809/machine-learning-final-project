import { Component, HostListener, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { PHASES } from './pages/crisp/crisp-data';
import { AuthService } from './services/auth.service';

@Component({
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  selector: 'app-root',
  templateUrl: './app.html',
})
export class App {
  protected readonly title = signal('Night Shift');
  protected readonly showScrollTop = signal(false);
  protected readonly phases = PHASES;
  protected readonly auth = inject(AuthService);

  constructor() {
    this.auth.load();
  }

  @HostListener('window:scroll')
  onScroll(): void {
    this.showScrollTop.set(window.scrollY > 400);
  }

  scrollToTop(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}
