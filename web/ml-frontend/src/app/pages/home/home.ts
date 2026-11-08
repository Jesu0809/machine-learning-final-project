import { Component, inject, signal } from '@angular/core';
import { PercentPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ResultsService } from '../../services/results.service';
import { BrainService } from '../../services/brain.service';
import { Results } from '../../models/results.model';
import { NightRow } from '../../models/brain.model';

@Component({
  selector: 'app-home',
  imports: [RouterLink, PercentPipe],
  templateUrl: './home.html',
})
export class Home {
  private readonly resultsService = inject(ResultsService);
  private readonly brainService = inject(BrainService);
  protected readonly results = signal<Results | null>(null);
  protected readonly nightRows = signal<NightRow[]>([]);

  protected readonly survivors = ['Novice', 'Normal', 'Expert'];

  protected readonly policies = [
    { id: 'trained', name: 'Trained network', detail: 'PPO, learned from rewards' },
    { id: 'scripted', name: 'Hand-written hunter', detail: 'same senses, rules instead of learning' },
    { id: 'random', name: 'Random hunter', detail: 'the lower baseline' },
  ];

  protected readonly objectives = [
    {
      title: 'Model the hunt as a Markov decision process: what he perceives (100 inputs, never the player\'s position), 17 actions and the rewards, on an abstract map of the circus\'s 13 rooms.',
      link: '/ml/concepts', linkText: 'Concepts and model',
    },
    {
      title: 'Train the policy with PPO against a simulated survivor with a different personality every night, improving it version by version (v1 to v19).',
      link: '/ml/training', linkText: 'Training',
    },
    {
      title: 'Evaluate it against a hand-written hunter and a random one, 400 nights per survivor skill, with 95% confidence intervals.',
      link: '/ml/metrics', linkText: 'Metrics',
    },
    {
      title: 'Put the trained network in a 3D game of seven nights, on the desktop and in the browser, where the player can watch it decide.',
      link: '/ml/evidence', linkText: 'Evidence in the game',
    },
    {
      title: 'Publish a website that explains the model and lets anyone ask the real network what it would do.',
      link: '/ml/application', linkText: 'Try the brain',
    },
  ];

  protected readonly method = [
    { name: 'Conceive', when: 'Week 1', text: 'The course brief, the choice of a horror game with an ML-Agents enemy, and the hunt as a decision problem: states, actions and rewards.' },
    { name: 'Design', when: 'Weeks 1–2', text: 'The 13-room graph, the observations he is allowed, the reward shaping, the simulated survivor and the split between brain (learned) and body (scripted).' },
    { name: 'Implement', when: 'Weeks 2–4', text: 'The simulation and 32 parallel training hunts, versions v1 to v19, the 3D circus built in code, Pockets, the seven nights and the website.' },
    { name: 'Operate', when: 'Week 5', text: 'Evaluation over 400 nights per cell, the browser build on Render, playtests, the threat level and the fixes they called for.' },
  ];

  protected readonly products = [
    { name: 'Software: the game', text: 'Night Shift for the desktop (Unity 6) and in the browser (WebGL), with seven nights, an encore and a show of the day.' },
    { name: 'Prototype: the trained model', text: 'freddy_v19.onnx, the PPO policy the game runs, with its training configuration and curves.' },
    { name: 'Web development', text: 'This site (Angular and Flask on Render): play, read how the brain works, and query the real network.' },
    { name: 'Technical document', text: 'The design of the decision problem, the training log of every version and the evaluation, in the repository\'s docs folder.' },
    { name: 'Digital content', text: 'The game\'s story (an intro film and the clown\'s speeches), told on the Story page.' },
    { name: 'Source code', text: 'The whole project, including the tools that sculpt and voice Pockets, in a public repository.' },
  ];

  protected readonly shots = [
    { file: 'intro-sign.jpg', caption: 'The intro: the guard drives up to the closed circus in the rain' },
    { file: 'intro-arrive.jpg', caption: 'The front doors, after a voicemail tells the story of his new brain' },
    { file: 'hall.jpg', caption: 'The ticket lobby, just past the chained doors' },
    { file: 'pockets.jpg', caption: 'Pockets wakes up in the Big Ring when a night begins' },
    { file: 'call.jpg', caption: 'Night 1: locked in the watch office while he explains the rules over the speakers' },
    { file: 'inspector.jpg', caption: 'AI inspector (Tab): what the network considered at its last decision' },
    { file: 'replay.jpg', caption: 'End of the night: how his brain hunted you, decision by decision' },
  ];

  constructor() {
    this.resultsService.getResults().subscribe({ next: (r) => this.results.set(r) });
    this.brainService.getModelInfo().subscribe({ next: (m) => this.nightRows.set(m.nights) });
  }

  protected rate(policy: string, survivor: string): number {
    const run = this.results()?.runs.find((r) => r.policy === policy && r.survivor === survivor);
    return run ? run.catch_rate : 0;
  }

  /** In-page links: scroll without changing the route (the fixed navbar is taken into account by CSS). */
  protected jump(event: Event, id: string): void {
    event.preventDefault();
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}
