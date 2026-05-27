import './index.css';
import { loadPullRequests } from './data/pull-requests';
import { appView } from './ui/app-view';

const appContainer = document.querySelector<HTMLDivElement>('#app');

if (!appContainer) {
  throw new Error('Missing #app root element');
}

const render = (content: Node): void => {
  appContainer.replaceChildren(content);
};

const pullRequests = await loadPullRequests();
render(appView(window.versions, pullRequests));
