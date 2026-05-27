import './index.css';
import { appView } from './ui/app-view';

const appContainer = document.querySelector<HTMLDivElement>('#app');

if (!appContainer) {
  throw new Error('Missing #app root element');
}

const render = (content: Node): void => {
  appContainer.replaceChildren(content);
};

render(appView(window.versions));
