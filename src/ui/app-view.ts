import { div, p, text } from './lib';
import type { AdoGitPullRequest } from '../data/pull-requests';
import { prsView } from './prs-view';

export const appView = (
  pullRequests: ReadonlyArray<AdoGitPullRequest>,
): HTMLDivElement =>
  div(
    prsView(pullRequests),
  );
