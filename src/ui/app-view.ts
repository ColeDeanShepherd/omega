import { _ } from './lib';

type VersionsApi = {
  node: () => string;
  chrome: () => string;
  electron: () => string;
};

declare global {
  interface Window {
    versions: VersionsApi;
  }
}

export const appView = () =>
  _.p([
    _.text(
      `This app is using Chrome (v${window.versions.chrome()}), Node.js (v${window.versions.node()}), and Electron (v${window.versions.electron()})`,
    ),
  ]);
