import { div, h2, li, small, strong, ul } from './lib';
import type {
	AdoGitPullRequest,
	PullRequestReviewer,
	PullRequestStatus,
	ReviewerVote,
} from '../data/pull-requests';

const summarizeReviewers = (reviewers: ReadonlyArray<PullRequestReviewer>): string => {
	const counts = reviewers.reduce(
		(result, reviewer) => ({
			...result,
			[reviewer.vote]: result[reviewer.vote] + 1,
		}),
		{ approved: 0, waiting: 0, rejected: 0 } as Record<ReviewerVote, number>,
	);

	return `${counts.approved} approved, ${counts.waiting} waiting, ${counts.rejected} rejected`;
};

const formatStatus = (status: PullRequestStatus, isDraft: boolean): string => {
	if (isDraft) {
		return 'draft';
	}

	return status;
};

const formatUpdated = (updatedAt: string): string => new Date(updatedAt).toLocaleDateString();

const prListItem = (pr: AdoGitPullRequest): HTMLLIElement =>
	li(
		strong(`#${pr.id} ${pr.title}`),
		' ',
		small(
			`[${formatStatus(pr.status, pr.isDraft)}] ${pr.repository} ${pr.sourceBranch} -> ${pr.targetBranch} | ${pr.author} | ${summarizeReviewers(pr.reviewers)} | updated ${formatUpdated(pr.updatedAt)}`,
		),
	);

export const prsView = (
	pullRequests: ReadonlyArray<AdoGitPullRequest>,
): HTMLDivElement =>
	div(
		h2('Pull Requests'),
		ul(...pullRequests.map(prListItem)),
	);
