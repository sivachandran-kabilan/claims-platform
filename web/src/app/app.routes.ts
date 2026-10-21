import { inject } from '@angular/core';
import { Routes } from '@angular/router';
import { AuthService } from './core/auth.service';
import { roleGuard } from './core/role.guard';

/**
 * Feature pages are lazy-loaded. Guards are UX only; the API enforces the role matrix.
 * Every leaf route uses pathMatch 'full': with the default prefix match, 'claims' (Claimant-only) also
 * matched '/claims/<id>' and its canMatch guard ran first, bouncing staff and managers away from claims.
 */
export const routes: Routes = [
  { path: 'login', pathMatch: 'full', loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage) },
  { path: 'register', pathMatch: 'full', loadComponent: () => import('./features/auth/register.page').then((m) => m.RegisterPage) },

  // Claimant
  {
    path: 'claims', pathMatch: 'full',
    canMatch: [roleGuard('Claimant')],
    loadComponent: () => import('./features/claimant/my-claims.page').then((m) => m.MyClaimsPage),
  },
  {
    path: 'claims/new', pathMatch: 'full',
    canMatch: [roleGuard('Claimant')],
    loadComponent: () => import('./features/claimant/new-claim.page').then((m) => m.NewClaimPage),
  },
  {
    path: 'notifications', pathMatch: 'full',
    canMatch: [roleGuard('Claimant')],
    loadComponent: () => import('./features/claimant/notifications.page').then((m) => m.NotificationsPage),
  },

  // Shared claim detail: every role (claimants only see their own; the API returns 404 otherwise)
  {
    path: 'claims/:id', pathMatch: 'full',
    canMatch: [roleGuard()],
    loadComponent: () => import('./features/claims/claim-detail.page').then((m) => m.ClaimDetailPage),
  },

  // Staff and managers
  {
    path: 'staff/queue', pathMatch: 'full',
    canMatch: [roleGuard('ClaimStaff', 'ClaimManager')],
    loadComponent: () => import('./features/staff/queue.page').then((m) => m.QueuePage),
  },
  {
    path: 'staff/claims', pathMatch: 'full',
    canMatch: [roleGuard('ClaimStaff', 'ClaimManager')],
    loadComponent: () => import('./features/staff/all-claims.page').then((m) => m.AllClaimsPage),
  },

  // Managers
  {
    path: 'manager/dashboard', pathMatch: 'full',
    canMatch: [roleGuard('ClaimManager')],
    loadComponent: () => import('./features/manager/dashboard.page').then((m) => m.DashboardPage),
  },

  { path: '', pathMatch: 'full', redirectTo: () => inject(AuthService).homeRoute() },
  { path: '**', pathMatch: 'full', redirectTo: '' },
];
