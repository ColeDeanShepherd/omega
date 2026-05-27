export interface IDataSource {
  activate: () => void;
  subscribe: (onDataReloaded: (data: unknown) => void) => () => void;
}

export class DataSource<T> implements IDataSource {
  private _data: T | null = null;
  private subscribers = new Set<(data: T) => void>();
  private activated = false;

  constructor(
    public load: () => Promise<T>,
    public reloadIntervalMs: number
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
      this._data = await this.load();

      for (const subscriber of this.subscribers) {
        subscriber(this._data);
      }
    };

    void reload();
    setInterval(() => {
      void reload();
    }, this.reloadIntervalMs);
  }
}