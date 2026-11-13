import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { ContentBlocks } from '../crisp/crisp-phase';
import { DOCS } from './docs-data';

@Component({
  selector: 'app-doc-page',
  imports: [RouterLink, ContentBlocks],
  template: `
    <div class="container py-5">
      @if (doc(); as d) {
        <nav aria-label="breadcrumb">
          <ol class="breadcrumb"><li class="breadcrumb-item"><a routerLink="/">Home</a></li>
            <li class="breadcrumb-item">Documentation</li><li class="breadcrumb-item active" aria-current="page">{{ d.title }}</li></ol>
        </nav>
        <div class="row g-4">
          <aside class="col-lg-3 d-none d-lg-block">
            <div class="list-group sticky-top small" style="top: 80px">
              @for (x of docs; track x.slug) {
                <a class="list-group-item list-group-item-action" [class.active]="x.slug === d.slug" [routerLink]="['/docs', x.slug]">{{ x.title }}</a>
              }
            </div>
          </aside>
          <div class="col-lg-9">
            <h1>{{ d.title }}</h1>
            <p class="lead mb-5">{{ d.lead }}</p>
            <app-content-blocks [blocks]="d.blocks" />
          </div>
        </div>
      } @else {
        <h1>Page not found</h1><a routerLink="/">Home</a>
      }
    </div>
  `,
})
export class DocPage {
  protected readonly docs = DOCS;
  private readonly slug = toSignal(inject(ActivatedRoute).paramMap.pipe(map((p) => p.get('page'))));
  protected readonly doc = computed(() => DOCS.find((d) => d.slug === this.slug()) ?? null);
}
