import type { RRule } from 'rrule';

import { RecurringTaskRunner } from './recurring-task';

export interface IDataSource {
  activate: () => void;
  subscribe: (onDataReloaded: (data: unknown) => void) => () => void;
  deactivate?: () => void;
}

export class DataSource<T> implements IDataSource {
  private _data: T | null = null;
  private subscribers = new Set<(data: T) => void>();
  private activated = false;
  private intervalHandle: ReturnType<typeof setInterval> | null = null;
  private recurringRunner: RecurringTaskRunner | null = null;

  constructor(
    public load: () => Promise<T>,
    public recurringRules: ReadonlyArray<RRule>,
  ) {}

  getData(): T | null { return this._data; }

  subscribe(onDataReloaded: (data: T) => void): () => void {
    this.subscribers.add(onDataReloaded);

    return () => {
      this.subscribers.delete(onDataReloaded);
    };
  }

  activate(): void {
    if (this.activated) {
      return;
    }

    this.activated = true;
    
    const reload = async (): Promise<void> => {
      try {
        this._data = await this.load();

        for (const subscriber of this.subscribers) {
          subscriber(this._data);
        }
      } catch (error) {
        console.error('Data source reload failed.', error);
      }
    };

    void reload();

    this.recurringRunner = new RecurringTaskRunner(
      this.recurringRules,
      reload,
      (error: unknown) => {
        console.error('Recurring data source task failed.', error);
      },
    );
    this.recurringRunner.start();
  }

  deactivate(): void {
    this.activated = false;

    if (this.intervalHandle) {
      clearInterval(this.intervalHandle);
      this.intervalHandle = null;
    }

    if (this.recurringRunner) {
      this.recurringRunner.stop();
      this.recurringRunner = null;
    }
  }
}