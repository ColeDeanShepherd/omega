import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

import type {
  AdoGitPullRequest,
  PullRequestStatus,
  ReviewerVote,
} from '../plugins/omega/pull-requests';

const execFileAsync = promisify(execFile);

type AzRepoPullRequestReviewer = Readonly<{
  displayName?: string;
  uniqueName?: string;
  vote?: number;
}>;

type AzRepoPullRequest = Readonly<{
  pullRequestId?: number;
  title?: string;
  repository?: {
    name?: string;
  };
  status?: PullRequestStatus;
  sourceRefName?: string;
  targetRefName?: string;
  createdBy?: {
    displayName?: string;
    uniqueName?: string;
  };
  isDraft?: boolean;
  creationDate?: string;
  closedDate?: string;
  reviewers?: ReadonlyArray<AzRepoPullRequestReviewer>;
}>;

const normalizeBranch = (refName: string | undefined): string =>
  (refName ?? '').replace('refs/heads/', '');

const mapVote = (vote: number | undefined): ReviewerVote => {
  if ((vote ?? 0) > 0) {
    return 'approved';
  }

  if ((vote ?? 0) <= -10) {
    return 'rejected';
  }

  return 'waiting';
};

const mapReviewers = (
  reviewers: ReadonlyArray<AzRepoPullRequestReviewer> | undefined,
): AdoGitPullRequest['reviewers'] =>
  (reviewers ?? []).map((reviewer) => ({
    displayName: reviewer.displayName ?? reviewer.uniqueName ?? 'Unknown reviewer',
    vote: mapVote(reviewer.vote),
  }));

const toAppPullRequest = (pullRequest: AzRepoPullRequest): AdoGitPullRequest => ({
  id: pullRequest.pullRequestId ?? 0,
  title: pullRequest.title ?? '(untitled)',
  repository: pullRequest.repository?.name ?? 'unknown-repo',
  status: pullRequest.status ?? 'active',
  sourceBranch: normalizeBranch(pullRequest.sourceRefName),
  targetBranch: normalizeBranch(pullRequest.targetRefName),
  author: pullRequest.createdBy?.displayName ?? pullRequest.createdBy?.uniqueName ?? 'Unknown author',
  isDraft: pullRequest.isDraft ?? false,
  updatedAt:
    pullRequest.closedDate ??
    pullRequest.creationDate ??
    new Date().toISOString(),
  reviewers: mapReviewers(pullRequest.reviewers),
});

const runAzCli = async (args: ReadonlyArray<string>): Promise<string> => {
  try {
    const { stdout } = await execFileAsync('az', args, {
      maxBuffer: 10 * 1024 * 1024,
    });

    return stdout;
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    throw new Error(`Azure CLI command failed: az ${args.join(' ')}\n${message}`);
  }
};

const ensureAzureDevOpsExtension = async (): Promise<void> => {
  try {
    await runAzCli(['extension', 'show', '--name', 'azure-devops', '-o', 'none']);
  } catch {
    await runAzCli(['extension', 'add', '--name', 'azure-devops', '--only-show-errors']);
  }
};

const getCurrentAzureCliUser = async (): Promise<string> => {
  const output = await runAzCli(['account', 'show', '--query', 'user.name', '-o', 'tsv']);
  return output.trim();
};

const loadProjectPullRequests = async (
  organization: string,
  creator: string,
  project: string,
): Promise<ReadonlyArray<AdoGitPullRequest>> => {
  const output = await runAzCli([
    'repos',
    'pr',
    'list',
    '--organization',
    organization,
    '--project',
    project,
    '--creator',
    creator,
    '--status',
    'active',
    '-o',
    'json',
  ]);

  const parsed = JSON.parse(output) as AzRepoPullRequest[];
  return parsed.map(toAppPullRequest);
};

export const loadAdoPullRequests = async (
  organization: string,
  projects: ReadonlyArray<string>,
): Promise<ReadonlyArray<AdoGitPullRequest>> => {
  await ensureAzureDevOpsExtension();
  const creator = await getCurrentAzureCliUser();
  const pullRequestsByProject = await Promise.all(
    projects.map((project) => loadProjectPullRequests(organization, creator, project)),
  );

  return pullRequestsByProject.flat();
};