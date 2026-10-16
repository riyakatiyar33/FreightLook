import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

/** Feature areas are lazy-loaded; `data.title` / `data.breadcrumb` drive the header. */
const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'dashboard',
    title: 'Dashboard · FreightLook',
    data: { title: 'Dashboard', breadcrumb: ['Overview', 'Dashboard'] },
    loadChildren: () => import('./features/dashboard/dashboard-module').then(m => m.DashboardModule),
  },
  {
    path: 'rates',
    title: 'Rate Search · FreightLook',
    data: { title: 'Rate Search', breadcrumb: ['Rates', 'Rate Search'] },
    loadChildren: () => import('./features/rate-search/rate-search-module').then(m => m.RateSearchModule),
  },
  {
    path: 'upload',
    title: 'Upload Rate Sheet · FreightLook',
    data: { title: 'Upload Rate Sheet', breadcrumb: ['Rates', 'Upload Rate Sheet'] },
    loadChildren: () => import('./features/upload-rate-sheet/upload-rate-sheet-module').then(m => m.UploadRateSheetModule),
  },
  { path: '**', redirectTo: 'dashboard' },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
