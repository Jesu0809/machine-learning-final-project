import { Component, OnDestroy, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CALLS, INTRO, INTRO_CARDS, NIGHT_CHANGES, NOTES } from '../../data/story';

@Component({
  selector: 'app-story',
  imports: [RouterLink],
  templateUrl: './story.html',
})
export class Story implements OnDestroy {
  protected readonly intro = INTRO;
  protected readonly cards = INTRO_CARDS;
  protected readonly calls = CALLS;
  protected readonly notes = NOTES;
  protected readonly changes = NIGHT_CHANGES;
  protected readonly night = signal(0);

  /** The line playing now, e.g. "intro:2" or "night1:0". */
  protected readonly playing = signal('');
  private readonly audio = new Audio();
  private queue: { prefix: string; line: number }[] = [];

  constructor() {
    this.audio.onended = () => this.next();
  }

  ngOnDestroy(): void {
    this.stop();
  }

  protected selectNight(index: number): void {
    this.stop();
    this.night.set(index);
  }

  /** Plays one line, or every line from it to the end when `all` is set. Clicking the playing line stops it. */
  protected play(prefix: string, line: number, count: number, all = false): void {
    if (this.playing() === `${prefix}:${line}`) {
      this.stop();
      return;
    }
    this.queue = [];
    const last = all ? count : line + 1;
    for (let i = line; i < last; i++) {
      this.queue.push({ prefix, line: i });
    }
    this.next();
  }

  protected stop(): void {
    this.queue = [];
    this.audio.pause();
    this.playing.set('');
  }

  private next(): void {
    const item = this.queue.shift();
    if (!item) {
      this.playing.set('');
      return;
    }
    this.playing.set(`${item.prefix}:${item.line}`);
    this.audio.src = `audio/${item.prefix}_${String(item.line + 1).padStart(2, '0')}.m4a`;
    this.audio.play().catch(() => this.playing.set(''));
  }

  protected noteTitle(note: string): string {
    return note.split('\n')[0];
  }

  protected noteBody(note: string): string[] {
    return note.split('\n').slice(1);
  }
}
