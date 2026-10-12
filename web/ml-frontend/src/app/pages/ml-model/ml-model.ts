import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BrainService } from '../../services/brain.service';
import { ModelInfo } from '../../models/brain.model';

@Component({
  selector: 'app-ml-model',
  imports: [RouterLink],
  templateUrl: './ml-model.html',
})
export class MlModel {
  private readonly brainService = inject(BrainService);
  protected readonly info = signal<ModelInfo | null>(null);
  protected readonly error = signal('');

  constructor() {
    this.brainService.getModelInfo().subscribe({
      next: (info) => this.info.set(info),
      error: () => this.error.set('Could not reach the API. Is Flask running?'),
    });
  }

  protected neighborNames(room: number): string {
    const info = this.info();
    return info ? info.neighbors[room].map((n) => info.rooms[n]).join(', ') : '';
  }
}
