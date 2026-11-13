import { Component, inject, signal } from '@angular/core';
import { DecimalPipe, PercentPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { BrainService } from '../../services/brain.service';
import { BrainDecision, BrainState, ModelInfo } from '../../models/brain.model';

// Rooms of the map (web/freddy_brain.py ROOMS): 0 Watch Office, 1 Prop Workshop, 2 Dressing Rooms,
// 3 Backstage Hall, 4 Trunk Store, 5 Concessions, 6 The Big Ring, 7 Promenade, 8 Birthday Tent,
// 9 The Midway, 10 Ticket Lobby, 11 Ringside Lounge, 12 Winter Garden.
const ROOM_COUNT = 13;
const ALL_BUT_STAGE = [0, 1, 2, 3, 4, 5, 7, 8, 9, 10, 11, 12];

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

  // Form fields. Noise levels go from 0 (nothing) to 3 (very loud: a toy, a surprise box, the breaker or the exit).
  // "fuses" is the API's name for the power cells in place (0-3).
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
  protected notChecked: boolean[] = Array(ROOM_COUNT).fill(true);

  protected readonly presets: Preset[] = [
    {
      label: 'Just woke up',
      hint: 'Asleep in the Big Ring at the start of the night: no clues, every room unchecked.',
      state: { freddy_room: 6, rooms_not_checked: ALL_BUT_STAGE },
    },
    {
      label: 'A noise in Concessions',
      hint: 'Someone runs (or clicks a cell in) in Concessions, across the circus, right after he woke up.',
      state: { freddy_room: 6, noises: { '5': 2 }, rooms_not_checked: ALL_BUT_STAGE },
    },
    {
      label: 'Player vanished in the Trunk Store',
      hint: 'He saw you go into the Trunk Store a moment ago and now he is there.',
      state: { freddy_room: 4, last_seen_room: 4, seconds_since_seen: 4, rooms_not_checked: [] },
    },
    {
      label: 'Lost on the Promenade',
      hint: 'No clues for a while; he has not checked the backstage wing.',
      state: { freddy_room: 7, night_progress: 0.4, rooms_not_checked: [0, 1, 2, 4] },
    },
    {
      label: 'The exit lever in the Ticket Lobby',
      hint: 'Every cell is in and someone is working the front doors\' lever, two rooms away.',
      state: { freddy_room: 7, noises: { '10': 3 }, fuses: 3, rooms_not_checked: [] },
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
    this.notChecked = Array.from({ length: ROOM_COUNT }, (_, i) => stale.includes(i));
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
