// Entry point of the Electron renderer process.

import '../index.css';

import { appViewInstances } from '../app-view-instances';

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