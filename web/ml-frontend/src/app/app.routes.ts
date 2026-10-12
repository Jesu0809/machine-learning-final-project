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

export const routes: Routes = [
  { path: '', component: Home, pathMatch: 'full' },
  { path: 'play', component: Play },
  { path: 'ml/concepts', component: MlConcepts },
  { path: 'ml/model', component: MlModel },
  { path: 'ml/training', component: MlTraining },
  { path: 'ml/metrics', component: MlMetrics },
  { path: 'ml/application', component: MlApplication },
  { path: 'ml/evidence', component: MlEvidence },
  { path: 'ml/experiments', component: MlExperiments },
  { path: 'story', component: Story },
  { path: 'credits', component: Credits },
  { path: '**', component: NotFound },
];
