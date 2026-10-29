import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ResultsService } from '../../services/results.service';
import { Runs } from '../../models/results.model';

@Component({
  selector: 'app-ml-evidence',
  imports: [RouterLink],
  templateUrl: './ml-evidence.html',
})
export class MlEvidence {
  private readonly resultsService = inject(ResultsService);
  protected readonly runs = signal<Runs | null>(null);

  protected readonly places = [
    {
      image: 'inspector.jpg',
      title: 'AI inspector (Tab)',
      text: 'In the middle of the game, Tab opens the network\'s live view: the probability of each of the 15 actions, what he heard, where he is going and why. The bars are computed by a C# forward pass of the same weights.',
    },
    {
      image: 'replay.jpg',
      title: 'Decision replay',
      text: 'When the night ends, the game shows the map with every decision his brain made, how sure it was and what it knew at that moment.',
    },
    {
      image: 'call.jpg',
      title: 'Story and notes',
      text: 'The phone calls and the lab notes quote the real training: the reward curve, the 66% vs 99% comparison and how often he senses.',
    },
    {
      image: 'hall.jpg',
      title: 'Behavior you can feel',
      text: 'He goes to the noise, checks the rooms next to it, opens the locker you hide in more often than the empty one, and senses when you are afraid. A thrown horn fools him because the network cannot tell a noise from you.',
    },
  ];

  protected readonly files = [
    ['unity/Assets/Scripts/Core/FreddyObservations.cs', 'The 64 observations'],
    ['unity/Assets/Scripts/Core/FreddyActions.cs', 'The 15 actions and the action mask'],
    ['unity/Assets/Scripts/Core/FreddyRewards.cs', 'The reward function'],
    ['unity/Assets/Scripts/Brain/FreddyAgent.cs', 'The ML-Agents Agent'],
    ['unity/Assets/Scripts/Core/PolicyProbe.cs', 'Forward pass for the inspector'],
    ['unity/Assets/ML/Models/freddy_v10.onnx', 'The trained model used in the game'],
    ['training/config/freddy_ppo.yaml', 'PPO hyperparameters'],
    ['training/evaluation/results.json', 'Evaluation results'],
    ['docs/ml-design.md', 'Full design of the learning problem'],
    ['docs/training-log.md', 'Log of every training run'],
  ];

  constructor() {
    this.resultsService.getRuns().subscribe({ next: (r) => this.runs.set(r) });
  }
}
