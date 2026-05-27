import type { WebContents } from 'electron';
import { loadPullRequests, AdoGitPullRequest } from './data/pull-requests';
import { DataSource } from './data/data-source';

export const startApp = (webContents: WebContents): void => {
  const trySendAppViewUpdate = (pullRequests: ReadonlyArray<AdoGitPullRequest>) => {
    if (!webContents.isDestroyed()) {
      webContents.send('app-view:update', pullRequests);
    }
  };
  
  const prDataSource =
    new DataSource<ReadonlyArray<AdoGitPullRequest>>(
      loadPullRequests,
      /* reloadIntervalMs: */ 60_000,
      /* onDataReloaded: */ trySendAppViewUpdate,
    );
  prDataSource.activate();
};