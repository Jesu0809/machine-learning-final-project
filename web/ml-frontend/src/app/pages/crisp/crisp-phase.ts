import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { Block, PHASES } from './crisp-data';

/** Renders the content blocks of a page (CRISP-ML phases and documentation). */
@Component({
  selector: 'app-content-blocks',
  imports: [RouterLink],
  template: `
    @for (b of blocks; track b.heading) {
      <section class="mb-5" [id]="anchor(b.heading)">
        <h2 class="h4 mb-3">{{ b.heading }}</h2>
        @for (p of b.text ?? []; track $index) { <p>{{ p }}</p> }
        @if (b.code) { <pre class="bg-black text-light p-3 rounded small mb-3"><code>{{ b.code }}</code></pre> }
        @if (b.bullets) { <ul>@for (li of b.bullets; track $index) { <li class="mb-1">{{ li }}</li> }</ul> }
        @if (b.table; as t) {
          <div class="table-responsive mb-2">
            <table class="table table-sm align-top">
              <thead><tr>@for (h of t.head; track h) { <th scope="col">{{ h }}</th> }</tr></thead>
              <tbody>@for (row of t.rows; track $index) { <tr>@for (cell of row; track $index) { <td>{{ cell }}</td> }</tr> }</tbody>
            </table>
          </div>
          @if (t.note) { <p class="small text-muted">{{ t.note }}</p> }
        }
        @if (b.image; as img) {
          <figure class="my-3">
            <img class="img-fluid rounded w-100" [src]="img.src" [alt]="img.alt" loading="lazy">
            <figcaption class="small text-muted mt-1">{{ img.caption }}</figcaption>
          </figure>
        }
        @if (b.callout; as c) {
          <div class="alert" [class.alert-info]="c.tone === 'info'" [class.alert-warning]="c.tone === 'warning'" [class.alert-success]="c.tone === 'success'">{{ c.text }}</div>
        }
        @if (b.links) {
          <p class="small mb-0">
            @for (l of b.links; track l.url) {
              @if (l.url.startsWith('/') && !l.url.startsWith('/api/')) {
                <a class="me-3" [routerLink]="l.url">{{ l.label }} →</a>
              } @else {
                <a class="me-3" [href]="l.url" target="_blank" rel="noopener">{{ l.label }} ↗</a>
              }
            }
          </p>
        }
      </section>
    }
  `,
  inputs: ['blocks'],
})
export class ContentBlocks {
  blocks: Block[] = [];

  protected anchor(heading: string): string {
    return heading.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');
  }
}

@Component({
  selector: 'app-crisp-phase',
  imports: [RouterLink, ContentBlocks],
  template: `
    <div class="container py-5">
      @if (phase(); as p) {
        <nav aria-label="breadcrumb">
          <ol class="breadcrumb">
            <li class="breadcrumb-item"><a routerLink="/">Home</a></li>
            <li class="breadcrumb-item"><a routerLink="/crisp-ml">CRISP-ML</a></li>
            <li class="breadcrumb-item active" aria-current="page">{{ p.number }}. {{ p.title }}</li>
          </ol>
        </nav>
        <div class="row g-4">
          <aside class="col-lg-3 d-none d-lg-block">
            <div class="list-group sticky-top small" style="top: 80px">
              @for (q of phases; track q.slug) {
                <a class="list-group-item list-group-item-action" [class.active]="q.slug === p.slug" [routerLink]="['/crisp-ml', q.slug]">{{ q.number }}. {{ q.title }}</a>
              }
            </div>
          </aside>
          <div class="col-lg-9">
            <p class="text-uppercase small text-info mb-1">CRISP-ML phase {{ p.number }} of 6</p>
            <h1 class="mb-2">{{ p.title }}</h1>
            <p class="lead">{{ p.question }}</p>
            <div class="card mb-5 border-info"><div class="card-body"><strong>In short:</strong> {{ p.summary }}</div></div>
            <app-content-blocks [blocks]="p.blocks" />
            <div class="d-flex justify-content-between flex-wrap gap-2">
              @if (previous(); as prev) { <a class="btn btn-outline-secondary" [routerLink]="['/crisp-ml', prev.slug]">← {{ prev.number }}. {{ prev.title }}</a> } @else { <span></span> }
              @if (next(); as nxt) { <a class="btn btn-primary" [routerLink]="['/crisp-ml', nxt.slug]">{{ nxt.number }}. {{ nxt.title }} →</a> }
              @else { <a class="btn btn-primary" routerLink="/studio">The information system →</a> }
            </div>
          </div>
        </div>
      } @else {
        <h1>Phase not found</h1>
        <a routerLink="/crisp-ml">Back to the six phases</a>
      }
    </div>
  `,
})
export class CrispPhase {
  protected readonly phases = PHASES;
  private readonly slug = toSignal(inject(ActivatedRoute).paramMap.pipe(map((p) => p.get('phase'))));
  protected readonly phase = computed(() => PHASES.find((p) => p.slug === this.slug()) ?? null);
  protected readonly previous = computed(() => PHASES[(this.phase()?.number ?? 1) - 2] ?? null);
  protected readonly next = computed(() => PHASES[this.phase()?.number ?? 99] ?? null);
}
