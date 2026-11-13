import { Routes } from '@angular/router';
import { Home } from './pages/home/home';
import { Play } from './pages/play/play';
import { MlConcepts } from './pages/ml-concepts/ml-concepts';
import { MlModel } from './pages/ml-model/ml-model';
import { MlTraining } from './pages/ml-training/ml-training';
import { MlMetrics } from './pages/ml-metrics/ml-metrics';
import { MlApplication } from './pages/ml-application/ml-application';
import { MlEvidence } from './pages/ml-evidence/ml-evidence';
import { MlExperiments } from './pages/ml-experiments/ml-experiments';
import { Story } from './pages/story/story';
import { Credits } from './pages/credits/credits';
import { NotFound } from './pages/not-found/not-found';
import { Login } from './pages/login/login';
import { roleGuard } from './services/role.guard';

export const routes: Routes = [
  { path: '', component: Home, pathMatch: 'full' },
  { path: 'play', component: Play },

  // Activity 1: the six CRISP-ML phases
  { path: 'crisp-ml', loadComponent: () => import('./pages/crisp/crisp-overview').then((m) => m.CrispOverview) },
  { path: 'crisp-ml/:phase', loadComponent: () => import('./pages/crisp/crisp-phase').then((m) => m.CrispPhase) },

  // Detailed ML evidence (linked from the phases)
  { path: 'ml/concepts', component: MlConcepts },
  { path: 'ml/model', component: MlModel },
  { path: 'ml/training', component: MlTraining },
  { path: 'ml/metrics', component: MlMetrics },
  { path: 'ml/application', component: MlApplication },
  { path: 'ml/evidence', component: MlEvidence },
  { path: 'ml/experiments', component: MlExperiments },

  // Documentation
  { path: 'docs/:page', loadComponent: () => import('./pages/docs/doc-page').then((m) => m.DocPage) },

  // Activity 2: the playtest studio (every studio API call is checked again on the server)
  { path: 'login', component: Login, data: { mode: 'login' } },
  { path: 'register', component: Login, data: { mode: 'register' } },
  { path: 'studio', loadComponent: () => import('./pages/studio/dashboard').then((m) => m.StudioDashboard), canActivate: [roleGuard()] },
  { path: 'studio/playtests', loadComponent: () => import('./pages/studio/playtests').then((m) => m.StudioPlaytests), canActivate: [roleGuard()] },
  { path: 'studio/reports', loadComponent: () => import('./pages/studio/reports').then((m) => m.StudioReports), canActivate: [roleGuard()] },
  { path: 'studio/reports/new', loadComponent: () => import('./pages/studio/report-form').then((m) => m.StudioReportForm), canActivate: [roleGuard()] },
  { path: 'studio/reports/:id', loadComponent: () => import('./pages/studio/report-detail').then((m) => m.StudioReportDetail), canActivate: [roleGuard()] },
  { path: 'studio/reports/:id/edit', loadComponent: () => import('./pages/studio/report-form').then((m) => m.StudioReportForm), canActivate: [roleGuard()] },
  { path: 'studio/model', loadComponent: () => import('./pages/studio/model-history').then((m) => m.StudioModel), canActivate: [roleGuard('designer')] },
  { path: 'studio/users', loadComponent: () => import('./pages/studio/users').then((m) => m.StudioUsers), canActivate: [roleGuard('admin')] },
  { path: 'studio/monitoring', loadComponent: () => import('./pages/studio/monitoring').then((m) => m.StudioMonitoring), canActivate: [roleGuard('admin')] },
  { path: 'studio/account', loadComponent: () => import('./pages/studio/account').then((m) => m.StudioAccount), canActivate: [roleGuard()] },

  { path: 'story', component: Story },
  { path: 'credits', component: Credits },
  { path: '**', component: NotFound },
];
