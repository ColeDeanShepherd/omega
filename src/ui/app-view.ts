import { div, p, text } from './lib';
import type { AdoGitPullRequest } from '../data/pull-requests';
import { prsView } from './prs-view';

type VersionsApi = Window['versions'];

export const appView = (
  versions: VersionsApi,
  pullRequests: ReadonlyArray<AdoGitPullRequest>,
): HTMLDivElement =>
  div(
    p(
      text(
        `This app is using Chrome (v${versions.chrome}), Node.js (v${versions.node}), and Electron (v${versions.electron})`,
      ),
    ),
    prsView(pullRequests),
  );
