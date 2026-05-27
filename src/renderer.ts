import './index.css';

import { loadPullRequests, AdoGitPullRequest } from './data/pull-requests';
import { DataSource } from './data/data-source';
import { appView } from './ui/app-view';

const appContainer = document.querySelector<HTMLDivElement>('#app');
if (!appContainer) {
  throw new Error('Missing #app root element');
}

const render = (content: Node): void => {
  appContainer.replaceChildren(content);
};

const prDataSource = new DataSource<ReadonlyArray<AdoGitPullRequest>>(
  loadPullRequests,
  60_000,
  (pullRequests) => {
    render(appView(window.versions, pullRequests));
  }
);

// TODO: move this out of renderer?
prDataSource.activate();
