// Entry point of the Electron renderer process.

import './index.css';

import { loadPullRequests, AdoGitPullRequest } from './data/pull-requests';
import { DataSource } from './data/data-source';
import { shuffledArray } from './ui/array-utils';
import { prsView } from './ui/prs-view';
import { IViewInstance, ViewInstance } from './ui/view-instance';

// #region View Instances

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

// #endregion View Instances

// #region Start Renderer

const appContainer = document.querySelector<HTMLDivElement>('#app');
if (!appContainer) {
  throw new Error('Missing #app root element');
}

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

// #endregion Start Renderer