import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ModelCard, Situation } from '../../models/studio.model';

export const ACTION_SENSE = 16;

export function emptySituation(): Situation {
  return {
    freddy_room: 6, noises: {}, last_seen_room: null, seconds_since_seen: 10, rooms_not_checked: [0, 1, 2, 3, 4, 5, 7, 8, 9, 10, 11, 12],
    sees_player: false, objectives: 0, night_progress: 0.3, searched_here: [], sense_ready: true, sensed_room: null, seconds_since_sensed: 10,
  };
}

/** Every action index with its name in this room, the ones the brain can take here first. */
export function actionsFor(card: ModelCard, room: number): { index: number; name: string; legal: boolean }[] {
  const rooms = card.rooms.length;
  const list: { index: number; name: string; legal: boolean }[] = [];
  for (let r = 0; r < rooms; r++) {
    const legal = r === room || card.neighbors[room].includes(r);
    list.push({ index: r, name: r === room ? 'Stay and listen' : 'Go to ' + card.rooms[r], legal });
  }
  for (let s = 0; s < 3; s++) {
    const spot = card.spots[room][s];
    list.push({ index: rooms + s, name: spot ? 'Search ' + spot : `Search hiding spot ${s} (none in this room)`, legal: !!spot });
  }
  list.push({ index: ACTION_SENSE, name: 'Sense (feel for fear)', legal: true });
  return [...list.filter((a) => a.legal), ...list.filter((a) => !a.legal)];
}

/** What Pockets knew: the fields the model reads, in the words of the game. */
@Component({
  selector: 'app-situation-form',
  imports: [FormsModule],
  template: `
    @let s = situation();
    @let m = card();
    <div class="row g-3">
      <div class="col-md-6">
        <label class="form-label small" for="sf-room">Pockets was in</label>
        <select id="sf-room" class="form-select" [(ngModel)]="s.freddy_room" (ngModelChange)="roomChanged()">
          @for (room of m.rooms; track room; let i = $index) { <option [ngValue]="i">{{ room }}</option> }
        </select>
      </div>
      <div class="col-md-6">
        <label class="form-label small" for="sf-progress">How far into the night: {{ (s.night_progress * 100).toFixed(0) }}%</label>
        <input id="sf-progress" type="range" class="form-range" min="0" max="1" step="0.05" [(ngModel)]="s.night_progress" (ngModelChange)="changed.emit()">
      </div>
      <div class="col-md-6">
        <label class="form-label small" for="sf-noise">He had just heard a noise in</label>
        <div class="input-group">
          <select id="sf-noise" class="form-select" [ngModel]="noiseRoom()" (ngModelChange)="setNoise($event, noiseLevel())">
            <option [ngValue]="null">no noise</option>
            @for (room of m.rooms; track room; let i = $index) { <option [ngValue]="i">{{ room }}</option> }
          </select>
          <select class="form-select" aria-label="How loud" [ngModel]="noiseLevel()" (ngModelChange)="setNoise(noiseRoom(), $event)" [disabled]="noiseRoom() === null">
            <option [ngValue]="1">footsteps</option>
            <option [ngValue]="2">running / a power cell</option>
            <option [ngValue]="3">a toy, a box, the breaker or the exit</option>
          </select>
        </div>
      </div>
      <div class="col-md-6">
        <label class="form-label small" for="sf-seen">He had last seen me in</label>
        <div class="input-group">
          <select id="sf-seen" class="form-select" [(ngModel)]="s.last_seen_room" (ngModelChange)="changed.emit()">
            <option [ngValue]="null">he had not seen me</option>
            @for (room of m.rooms; track room; let i = $index) { <option [ngValue]="i">{{ room }}</option> }
          </select>
          <input type="number" class="form-control" min="0" max="600" aria-label="Seconds before" [(ngModel)]="s.seconds_since_seen"
                 [disabled]="s.last_seen_room === null" (ngModelChange)="changed.emit()">
          <span class="input-group-text">s before</span>
        </div>
      </div>
      <div class="col-md-6">
        <label class="form-label small" for="sf-obj">Share of the night's objectives I had done: {{ (s.objectives * 100).toFixed(0) }}%</label>
        <input id="sf-obj" type="range" class="form-range" min="0" max="1" step="0.25" [(ngModel)]="s.objectives" (ngModelChange)="changed.emit()">
      </div>
      <div class="col-md-6 d-flex flex-column justify-content-end">
        <div class="form-check form-switch">
          <input class="form-check-input" type="checkbox" id="sf-sees" [(ngModel)]="s.sees_player" (ngModelChange)="changed.emit()">
          <label class="form-check-label small" for="sf-sees">He could see me at that moment</label>
        </div>
        <div class="form-check form-switch">
          <input class="form-check-input" type="checkbox" id="sf-sense" [(ngModel)]="s.sense_ready" (ngModelChange)="changed.emit()">
          <label class="form-check-label small" for="sf-sense">His sense was charged (he had not used it in a while)</label>
        </div>
      </div>
      <div class="col-12">
        <p class="form-label small mb-1">Rooms he had <strong>not</strong> checked for a long time (best guess)</p>
        <div class="d-flex flex-wrap gap-2">
          @for (room of m.rooms; track room; let i = $index) {
            @if (i !== s.freddy_room) {
              <div class="form-check">
                <input class="form-check-input" type="checkbox" [id]="'sf-stale' + i" [checked]="s.rooms_not_checked.includes(i)" (change)="toggleStale(i)">
                <label class="form-check-label small" [for]="'sf-stale' + i">{{ room }}</label>
              </div>
            }
          }
        </div>
      </div>
      @if (m.spots[s.freddy_room].length) {
        <div class="col-12">
          <p class="form-label small mb-1">Hiding spots in his room he had already opened</p>
          <div class="d-flex flex-wrap gap-3">
            @for (spot of m.spots[s.freddy_room]; track spot; let i = $index) {
              <div class="form-check">
                <input class="form-check-input" type="checkbox" [id]="'sf-spot' + i" [checked]="s.searched_here.includes(i)" (change)="toggleSpot(i)">
                <label class="form-check-label small" [for]="'sf-spot' + i">{{ spot }}</label>
              </div>
            }
          </div>
        </div>
      }
    </div>
  `,
})
export class SituationForm {
  readonly situation = input.required<Situation>();
  readonly card = input.required<ModelCard>();
  readonly changed = output<void>();

  protected noiseRoom(): number | null {
    const first = Object.keys(this.situation().noises)[0];
    return first === undefined ? null : Number(first);
  }

  protected noiseLevel(): number {
    const first = Object.values(this.situation().noises)[0];
    return first ?? 2;
  }

  protected setNoise(room: number | null, level: number): void {
    this.situation().noises = room === null ? {} : { [String(room)]: Number(level) };
    this.changed.emit();
  }

  protected roomChanged(): void {
    const s = this.situation();
    s.searched_here = [];
    s.rooms_not_checked = s.rooms_not_checked.filter((r) => r !== s.freddy_room);
    this.changed.emit();
  }

  protected toggleStale(room: number): void {
    const s = this.situation();
    s.rooms_not_checked = s.rooms_not_checked.includes(room) ? s.rooms_not_checked.filter((r) => r !== room) : [...s.rooms_not_checked, room];
    this.changed.emit();
  }

  protected toggleSpot(spot: number): void {
    const s = this.situation();
    s.searched_here = s.searched_here.includes(spot) ? s.searched_here.filter((x) => x !== spot) : [...s.searched_here, spot];
    this.changed.emit();
  }
}
