import { p, text } from './lib';

type VersionsApi = Window['versions'];

export const appView = (versions: VersionsApi) =>
  p(
    text(
      `This app is using Chrome (v${versions.chrome}), Node.js (v${versions.node}), and Electron (v${versions.electron})`,
    ),
  );
