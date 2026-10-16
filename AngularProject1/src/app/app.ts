import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconRegistry } from '@angular/material/icon';
import { MatSidenav } from '@angular/material/sidenav';
import { NavigationEnd, Router } from '@angular/router';
import { filter, map } from 'rxjs';

interface NavItem {
  path: string;
  icon: string;
  label: string;
}

interface PageInfo {
  title: string;
  breadcrumb: string[];
}

@Component({
  selector: 'app-root',
  standalone: false,
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  private readonly router = inject(Router);

  protected readonly navItems: NavItem[] = [
    { path: '/dashboard', icon: 'dashboard', label: 'Dashboard' },
    { path: '/rates', icon: 'travel_explore', label: 'Rate Search' },
    { path: '/upload', icon: 'upload_file', label: 'Upload Rate Sheet' },
  ];

  /** Below 960px the sidebar becomes an overlay drawer. */
  protected readonly isHandset = toSignal(
    inject(BreakpointObserver).observe('(max-width: 959.98px)').pipe(map(state => state.matches)),
    { initialValue: false },
  );

  /** Desktop: icon-only rail when collapsed. */
  protected readonly collapsed = signal(false);

  protected readonly page = toSignal(
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      map(() => this.readPageInfo()),
    ),
    { initialValue: { title: '', breadcrumb: [] } as PageInfo },
  );

  constructor() {
    inject(MatIconRegistry).setDefaultFontSetClass('material-symbols-outlined');
  }

  protected toggleNav(sidenav: MatSidenav): void {
    if (this.isHandset()) {
      sidenav.toggle();
    } else {
      this.collapsed.update(collapsed => !collapsed);
    }
  }

  protected closeIfHandset(sidenav: MatSidenav): void {
    if (this.isHandset()) {
      sidenav.close();
    }
  }

  private readPageInfo(): PageInfo {
    let route = this.router.routerState.snapshot.root;
    while (route.firstChild) {
      route = route.firstChild;
    }
    return {
      title: route.data['title'] ?? 'FreightLook',
      breadcrumb: route.data['breadcrumb'] ?? [],
    };
  }
}
