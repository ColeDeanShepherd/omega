import { DataSource } from '../../data-source';
import { shuffledArray } from '../../array-utils';
import { IViewInstance, ViewInstance } from '../../ui/view-instance';

import { IPlugin } from '../plugin';

import { loadPullRequests, AdoGitPullRequest } from './pull-requests';
import { prsView } from './prs-view';

const prDataSource =
  new DataSource<ReadonlyArray<AdoGitPullRequest>>(
    async () => {
      const pullRequests = (await loadPullRequests()).slice();
      return shuffledArray(pullRequests);
    },
    /* reloadIntervalMs: */ 1_000,
  );
const prsViewInstance = new ViewInstance(prDataSource, prsView);

const appViewInstances: IViewInstance[] = [prsViewInstance];

export const omegaPlugin: IPlugin = {
  viewInstances: appViewInstances,
};