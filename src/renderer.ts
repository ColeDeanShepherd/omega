import './index.css';
import { appView } from './ui/app-view';

const appContainer = document.getElementById('app')!;

const render = (content: Node) => {
  appContainer.replaceChildren(content);
};

render(appView());
