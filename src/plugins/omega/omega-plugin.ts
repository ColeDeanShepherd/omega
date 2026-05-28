import { DataSource } from '../../data-source';
import { shuffledArray } from '../../array-utils';
import { IViewInstance, ViewInstance } from '../../ui/view-instance';
import { RRule } from 'rrule';

import { IPlugin } from '../plugin';

import { loadPullRequests, AdoGitPullRequest, loadFakePullRequests } from './pull-requests';
import { prsView } from './prs-view';

const prDataSource =
  new DataSource<ReadonlyArray<AdoGitPullRequest>>(
    async () => {
      const pullRequests = (await loadFakePullRequests()).slice();
      return shuffledArray(pullRequests);
    },
    /* recurringRules: */ [new RRule({ freq: RRule.SECONDLY, interval: 1 })],
  );
const prsViewInstance = new ViewInstance(prDataSource, prsView);

const appViewInstances: IViewInstance[] = [
  //prsViewInstance
];

export const omegaPlugin: IPlugin = {
  viewInstances: appViewInstances,
};