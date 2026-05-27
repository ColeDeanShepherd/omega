import { DataSource, IDataSource } from '../data-source';

export interface IViewInstance {
  dataSource: IDataSource;
  render: () => HTMLElement;
}

export class ViewInstance<T> implements IViewInstance {
  constructor(
    public readonly dataSource: DataSource<T>,
    public readonly renderData: (data: T | null) => HTMLElement,
  ) {}

  render(): HTMLElement {
    const data = this.dataSource.getData();
    return this.renderData(data);
  }
}
