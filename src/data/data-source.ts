export class DataSource<T> {
  private _data: T | null = null;

  constructor(
    public load: () => Promise<T>,
    public reloadIntervalMs: number,
    private onDataReloaded: (data: T) => void
  ) {}

  getData(): T | null {
    return this._data;
  }

  activate(): void {
    const reload = async (): Promise<void> => {
      this._data = await this.load();
      this.onDataReloaded(this._data);
    };

    void reload();
    setInterval(() => {
      void reload();
    }, this.reloadIntervalMs);
  }
}