import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'map',
    loadComponent: () => import('./map/map').then((m) => m.MapComponent)
  }
];
