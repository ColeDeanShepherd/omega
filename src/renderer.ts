// Entry point of the Electron renderer process.

import './index.css';

import { loadPullRequests, AdoGitPullRequest } from './data/pull-requests';
import { DataSource, IDataSource } from './data/data-source';
import { prsView } from './ui/prs-view';


const appContainer = document.querySelector<HTMLDivElement>('#app');
if (!appContainer) {
  throw new Error('Missing #app root element');
}

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

export const appViewInstances: IViewInstance[] = [];

const shuffledArray = <T>(array: T[]): T[] => {
  const result = [...array];
  for (let i = result.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [result[i], result[j]] = [result[j], result[i]];
  }
  return result;
}

const loadAndShufflePullRequests = async (): Promise<ReadonlyArray<AdoGitPullRequest>> => {
  const pullRequests = (await loadPullRequests()).slice();
  return shuffledArray(pullRequests);
}

const prDataSource =
  new DataSource<ReadonlyArray<AdoGitPullRequest>>(
    loadAndShufflePullRequests,
    /* reloadIntervalMs: */ 1_000,
  );

const prsViewInstance = new ViewInstance(prDataSource, prsView);
appViewInstances.push(prsViewInstance);

const rerenderApp = () => {
  const content: HTMLElement[] = appViewInstances.map(instance => instance.render());
  appContainer.replaceChildren(...content);
};

// Get a distinct set of data sources from the view instances,
// and subscribe to changes on each of them.
// When any data source changes, re-render the entire app.
const dataSources = Array.from(new Set(appViewInstances.map(instance => instance.dataSource)));
dataSources.forEach(dataSource => dataSource.subscribe(rerenderApp));
dataSources.forEach(dataSource => dataSource.activate());