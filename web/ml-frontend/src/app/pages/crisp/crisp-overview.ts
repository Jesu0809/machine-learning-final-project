import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PHASES } from './crisp-data';

@Component({
  selector: 'app-crisp-overview',
  imports: [RouterLink],
  template: `
    <div class="container py-5">
      <nav aria-label="breadcrumb">
        <ol class="breadcrumb"><li class="breadcrumb-item"><a routerLink="/">Home</a></li><li class="breadcrumb-item active" aria-current="page">CRISP-ML</li></ol>
      </nav>
      <h1>The project in six CRISP-ML phases</h1>
      <p class="lead">CRISP-ML(Q) (Studer et al., 2021) extends CRISP-DM for machine learning: business and data understanding,
        data engineering, model engineering, evaluation, deployment, and monitoring and maintenance. Each phase below shows what we
        actually did for Pockets’ brain, with the decisions, figures and results behind it.</p>

      <div class="row g-3 my-4">
        @for (p of phases; track p.slug) {
          <div class="col-md-6 col-lg-4">
            <a class="card h-100 text-decoration-none" [routerLink]="['/crisp-ml', p.slug]">
              <div class="card-body">
                <div class="small text-info">Phase {{ p.number }}</div>
                <h2 class="h5">{{ p.title }}</h2>
                <p class="small text-muted mb-0">{{ p.question }}</p>
              </div>
            </a>
          </div>
        }
      </div>

      <div class="card"><div class="card-body">
        <h2 class="h5">How the phases connect to this site</h2>
        <div class="table-responsive">
          <table class="table table-sm small mb-0">
            <thead><tr><th>Phase</th><th>Main evidence</th><th>Where to see it working</th></tr></thead>
            <tbody>
              <tr><td>1. Business and data understanding</td><td>Objectives, success criteria, data sources</td><td><a routerLink="/ml/concepts">Concepts</a></td></tr>
              <tr><td>2. Data engineering</td><td>100 inputs, encoding, PCA, partitions</td><td><a routerLink="/ml/model">The model</a></td></tr>
              <tr><td>3. Model engineering</td><td>PPO vs baselines, 19 runs</td><td><a routerLink="/ml/training">Training</a>, <a routerLink="/ml/experiments">Experiments</a></td></tr>
              <tr><td>4. Evaluation</td><td>Catch rates with confidence intervals</td><td><a routerLink="/ml/metrics">Metrics</a></td></tr>
              <tr><td>5. Deployment</td><td>ONNX in the game, numpy on the server</td><td><a routerLink="/ml/application">Try the brain</a>, <a routerLink="/play">Play</a></td></tr>
              <tr><td>6. Monitoring and maintenance</td><td>Logs, drift, alerts, retraining rules</td><td><a routerLink="/studio">Playtest studio</a> (sign in)</td></tr>
            </tbody>
          </table>
        </div>
      </div></div>
    </div>
  `,
})
export class CrispOverview {
  protected readonly phases = PHASES;
}
