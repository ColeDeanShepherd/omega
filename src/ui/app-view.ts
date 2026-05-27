import { div, p, text } from './lib';
import { prsView } from './prs-view';

type VersionsApi = Window['versions'];

export const appView = (versions: VersionsApi) =>
  div(
    p(
      text(
        `This app is using Chrome (v${versions.chrome}), Node.js (v${versions.node}), and Electron (v${versions.electron})`,
      ),
    ),
    prsView(),
  );
