import { Component, inject, signal } from '@angular/core';
import { DecimalPipe, PercentPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { BrainService } from '../../services/brain.service';
import { BrainDecision, BrainState, ModelInfo } from '../../models/brain.model';

interface Preset {
  label: string;
  hint: string;
  state: Partial<BrainState>;
}

@Component({
  selector: 'app-ml-application',
  imports: [RouterLink, FormsModule, DecimalPipe, PercentPipe],
  templateUrl: './ml-application.html',
})
export class MlApplication {
  private readonly brainService = inject(BrainService);

  protected readonly info = signal<ModelInfo | null>(null);
  protected readonly decision = signal<BrainDecision | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal('');
  protected readonly showInputs = signal(false);

  // Form fields. Noise levels go from 0 (nothing) to 3 (very loud: a horn, the breaker or the exit panel).
  protected freddyRoom = 6;
  protected noiseRoom: number | null = null;
  protected noiseLevel = 2;
  protected lastSeenRoom: number | null = null;
  protected secondsSinceSeen = 10;
  protected seesPlayer = false;
  protected fuses = 0;
  protected nightProgress = 10;
  protected searchedHere: boolean[] = [false, false, false];
  protected senseReady = true;
  protected sensedRoom: number | null = null;
  protected notChecked: boolean[] = Array(9).fill(true);

  protected readonly presets: Preset[] = [
    {
      label: 'Just woke up',
      hint: 'On the stage at the start of the night: no clues, every room unchecked.',
      state: { freddy_room: 6, rooms_not_checked: [0, 1, 2, 3, 4, 5, 7, 8] },
    },
    {
      label: 'Heard a fuse in the kitchen',
      hint: 'Someone took a fuse next door, right after he woke up.',
      state: { freddy_room: 6, noises: { '3': 2 }, rooms_not_checked: [0, 1, 2, 3, 4, 5, 7, 8] },
    },
    {
      label: 'Player vanished in Storage',
      hint: 'He saw you go into Storage a moment ago and now he is there.',
      state: { freddy_room: 2, last_seen_room: 2, seconds_since_seen: 4, rooms_not_checked: [] },
    },
    {
      label: 'Lost in the Dining Hall',
      hint: 'No clues for a while; he has not checked the east side.',
      state: { freddy_room: 4, night_progress: 0.4, rooms_not_checked: [6, 7, 8] },
    },
    {
      label: 'A horn in the Exit Lobby',
      hint: 'A party horn goes off two rooms away.',
      state: { freddy_room: 4, noises: { '8': 3 }, rooms_not_checked: [] },
    },
  ];

  constructor() {
    this.brainService.getModelInfo().subscribe({
      next: (info) => {
        this.info.set(info);
        this.applyPreset(this.presets[0]);
      },
      error: () => this.error.set('Could not reach the API. Is Flask running?'),
    });
  }

  protected spotsHere(): string[] {
    return this.info()?.spots[this.freddyRoom] ?? [];
  }

  protected applyPreset(preset: Preset): void {
    const s = preset.state;
    this.freddyRoom = s.freddy_room ?? 6;
    const noise = Object.entries(s.noises ?? {})[0];
    this.noiseRoom = noise ? Number(noise[0]) : null;
    this.noiseLevel = noise ? noise[1] : 2;
    this.lastSeenRoom = s.last_seen_room ?? null;
    this.secondsSinceSeen = s.seconds_since_seen ?? 10;
    this.seesPlayer = s.sees_player ?? false;
    this.fuses = s.fuses ?? 0;
    this.nightProgress = Math.round((s.night_progress ?? 0.1) * 100);
    this.searchedHere = [false, false, false];
    this.senseReady = s.sense_ready ?? true;
    this.sensedRoom = s.sensed_room ?? null;
    const stale = s.rooms_not_checked ?? [];
    this.notChecked = Array.from({ length: 9 }, (_, i) => stale.includes(i));
    this.ask();
  }

  protected onRoomChange(): void {
    this.searchedHere = [false, false, false];
    this.ask();
  }

  protected ask(): void {
    const state: BrainState = {
      freddy_room: Number(this.freddyRoom),
      noises: this.noiseRoom !== null ? { [String(this.noiseRoom)]: Number(this.noiseLevel) } : {},
      last_seen_room: this.lastSeenRoom !== null ? Number(this.lastSeenRoom) : null,
      seconds_since_seen: Number(this.secondsSinceSeen),
      sees_player: this.seesPlayer,
      fuses: Number(this.fuses),
      night_progress: Number(this.nightProgress) / 100,
      searched_here: this.searchedHere.map((v, i) => (v ? i : -1)).filter((i) => i >= 0),
      sense_ready: this.senseReady,
      rooms_not_checked: this.notChecked.map((v, i) => (v ? i : -1)).filter((i) => i >= 0),
      sensed_room: this.sensedRoom !== null ? Number(this.sensedRoom) : null,
      seconds_since_sensed: 10,
    };
    this.loading.set(true);
    this.error.set('');
    this.brainService.decide(state).subscribe({
      next: (d) => {
        this.decision.set(d);
        this.loading.set(false);
      },
      error: (e) => {
        this.error.set(e?.error?.error ?? 'The brain did not answer.');
        this.loading.set(false);
      },
    });
  }
}
