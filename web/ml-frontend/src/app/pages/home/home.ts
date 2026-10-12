import { Component, inject, signal } from '@angular/core';
import { PercentPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ResultsService } from '../../services/results.service';
import { Results } from '../../models/results.model';

@Component({
  selector: 'app-home',
  imports: [RouterLink, PercentPipe],
  templateUrl: './home.html',
})
export class Home {
  private readonly resultsService = inject(ResultsService);
  protected readonly results = signal<Results | null>(null);

  protected readonly shots = [
    { file: 'intro-sign.jpg', caption: 'The intro: the guard arrives at the closed pizzeria in the rain' },
    { file: 'intro-arrive.jpg', caption: 'The front doors of the old pizzeria, after a voicemail tells the story of his new brain' },
    { file: 'menu.jpg', caption: 'Freddy asleep on the show stage' },
    { file: 'call.jpg', caption: 'Night 1: the phone call explains the rules while you explore' },
    { file: 'inspector.jpg', caption: 'AI inspector (Tab): what the network knows and how sure it is' },
    { file: 'replay.jpg', caption: 'End of the night: how his brain hunted you, decision by decision' },
    { file: 'door.jpg', caption: 'Close a door behind you and he has to break it down' },
    { file: 'jumpscare.jpg', caption: 'Caught' },
  ];

  constructor() {
    this.resultsService.getResults().subscribe({ next: (r) => this.results.set(r) });
  }

  protected rate(policy: string): number {
    const run = this.results()?.runs.find((r) => r.policy === policy && r.survivor === 'Normal');
    return run ? run.catch_rate : 0;
  }
}
