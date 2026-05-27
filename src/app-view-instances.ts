import { loadPullRequests, AdoGitPullRequest } from './data/pull-requests';
import { DataSource } from './data/data-source';
import { shuffledArray } from './ui/array-utils';
import { prsView } from './ui/prs-view';
import { IViewInstance, ViewInstance } from './ui/view-instance';

export const appViewInstances: IViewInstance[] = [];

const prDataSource =
  new DataSource<ReadonlyArray<AdoGitPullRequest>>(
    async () => {
      const pullRequests = (await loadPullRequests()).slice();
      return shuffledArray(pullRequests);
    },
    /* reloadIntervalMs: */ 1_000,
  );

const prsViewInstance = new ViewInstance(prDataSource, prsView);
appViewInstances.push(prsViewInstance);