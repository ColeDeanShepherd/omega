import type { WebContents } from 'electron';
import { loadPullRequests, AdoGitPullRequest } from './data/pull-requests';
import { DataSource } from './data/data-source';
import { appView } from './ui/app-view';

export interface IViewInstance {}

export class ViewInstance<T> implements IViewInstance {
  constructor(
    public readonly dataSource: DataSource<T>,
    public readonly render: (data: T) => HTMLElement,
  ) {}
}

export const appViewInstances: IViewInstance[] = [];

export const startApp = (webContents: WebContents): void => {
  // TODO: singleton WebContents?
  const trySendAppViewUpdate = (pullRequests: ReadonlyArray<AdoGitPullRequest>) => {
    if (!webContents.isDestroyed()) {
      webContents.send('app-view:update', pullRequests);
    }
  };
  
  const prDataSource =
    new DataSource<ReadonlyArray<AdoGitPullRequest>>(
      loadPullRequests,
      /* reloadIntervalMs: */ 60_000,
    );

  prDataSource.subscribe(trySendAppViewUpdate);
  
  const prsViewInstance = new ViewInstance(prDataSource, appView);
  appViewInstances.push(prsViewInstance);
  
  prDataSource.activate();
};