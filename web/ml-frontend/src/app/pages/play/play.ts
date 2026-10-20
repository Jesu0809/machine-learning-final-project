import { AfterViewInit, Component, ElementRef, inject, OnDestroy, signal, ViewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ResultsService } from '../../services/results.service';

interface UnityInstance {
  SetFullscreen(fullscreen: number): void;
  Quit(): Promise<void>;
}

const LOADER_ID = 'unity-loader';

declare function createUnityInstance(
  canvas: HTMLCanvasElement,
  config: Record<string, string>,
  onProgress: (progress: number) => void,
): Promise<UnityInstance>;

@Component({
  selector: 'app-play',
  imports: [RouterLink],
  templateUrl: './play.html',
})
export class Play implements AfterViewInit, OnDestroy {
  private readonly resultsService = inject(ResultsService);
  @ViewChild('canvas') private canvas?: ElementRef<HTMLCanvasElement>;

  protected readonly available = signal<boolean | null>(null);
  protected readonly progress = signal(0);
  protected readonly loaded = signal(false);
  protected readonly error = signal('');
  protected readonly gamepad = signal(false);
  protected readonly wide = signal(false);
  private instance: UnityInstance | null = null;
  /** True once the page is left: a game still loading is shut down as soon as it is ready. */
  private destroyed = false;

  ngAfterViewInit(): void {
    this.resultsService.getGameStatus().subscribe({
      next: (status) => {
        this.available.set(status.available);
        if (status.available) {
          setTimeout(() => this.load());
        }
      },
      error: () => this.available.set(false),
    });
  }

  /**
   * Leaving the page shuts the game down. Without this the old game kept running (its sound and
   * its phone call) and coming back started a second one on top.
   */
  ngOnDestroy(): void {
    this.destroyed = true;
    if (this.instance) {
      const instance = this.instance;
      this.instance = null;
      instance.Quit().catch(() => undefined);
    }
  }

  /** The game alone on the whole screen (Esc leaves full screen; press it again to pause). */
  protected fullscreen(): void {
    if (this.instance) {
      this.instance.SetFullscreen(1);
    } else {
      this.canvas?.nativeElement.requestFullscreen();
    }
  }

  /** Loads the Unity WebGL build served by Flask under /game. */
  private load(): void {
    if (this.destroyed) {
      return;
    }
    // The loader script is added once per visit to the site; later visits reuse it.
    if (document.getElementById(LOADER_ID)) {
      this.start();
      return;
    }
    const script = document.createElement('script');
    script.id = LOADER_ID;
    script.src = '/game/Build/game.loader.js';
    script.onload = () => this.start();
    script.onerror = () => {
      script.remove();
      this.error.set('The game loader could not be downloaded.');
    };
    document.body.appendChild(script);
  }

  private start(): void {
    if (!this.canvas || this.destroyed) {
      return;
    }
    createUnityInstance(this.canvas.nativeElement, {
        dataUrl: '/game/Build/game.data.gz',
        frameworkUrl: '/game/Build/game.framework.js.gz',
        codeUrl: '/game/Build/game.wasm.gz',
        streamingAssetsUrl: '/game/StreamingAssets',
        companyName: 'UdeC',
        productName: 'Night Shift',
        productVersion: '1.0',
      }, (p) => this.progress.set(Math.round(p * 100)))
        .then((instance) => {
          if (this.destroyed) {
            instance.Quit().catch(() => undefined);
            return;
          }
          this.instance = instance;
          this.loaded.set(true);
        })
        .catch((message) => this.error.set(String(message)));
  }
}
