import { Component, computed, signal } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { visibleClosures, type ClosureFilter, type ClosureStatus } from './closure-view';

interface Closure {
  id: string;
  organization: number;
  term: string;
  classCode: string;
  title: string;
  status: ClosureStatus;
  version: number;
  studentCount: number;
  approvedCount: number;
  failedCount: number;
  importedAt: string;
  closedAt: string | null;
}

type DashboardState = { kind: 'loading' | 'error' } | { kind: 'ready'; rows: readonly Closure[] };

@Component({
  selector: 'ft-dashboard',
  templateUrl: './dashboard.html',
})
class DashboardComponent {
  protected readonly state = signal<DashboardState>({ kind: 'loading' });
  protected readonly filter = signal<ClosureFilter>('all');
  protected readonly filters: readonly { value: ClosureFilter; label: string }[] = [
    { value: 'all', label: 'Todas' },
    { value: 'pending', label: 'Pendentes' },
    { value: 'closed', label: 'Encerradas' },
  ];
  protected readonly rows = computed(() => {
    const state = this.state();
    return state.kind === 'ready' ? state.rows : [];
  });
  protected readonly counts = computed(() => {
    const rows = this.rows();
    return {
      all: rows.length,
      pending: visibleClosures(rows, 'pending').length,
      closed: visibleClosures(rows, 'closed').length,
    };
  });
  protected readonly visible = computed(() => visibleClosures(this.rows(), this.filter()));
  protected readonly numberFormat = new Intl.NumberFormat('pt-BR');
  protected readonly dateFormat = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeZone: 'UTC' });

  protected formatDate(value: string): string {
    const timestamp = Date.parse(value);
    return Number.isFinite(timestamp) ? this.dateFormat.format(timestamp) : 'data indisponível';
  }

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.state.set({ kind: 'loading' });
    try {
      const response = await fetch('/api/v1/closures', {
        headers: { Accept: 'application/json' },
        credentials: 'omit',
        signal: AbortSignal.timeout(15_000),
      });
      if (!response.ok) throw new Error(`Unexpected HTTP status: ${response.status}`);
      const rows: unknown = await response.json();
      if (!Array.isArray(rows)) throw new Error('Expected a closure array');
      this.state.set({ kind: 'ready', rows });
    } catch {
      this.state.set({ kind: 'error' });
    }
  }
}

void bootstrapApplication(DashboardComponent);
