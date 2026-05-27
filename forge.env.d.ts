/// <reference types="@electron-forge/plugin-vite/forge-vite-env" />

import type { AdoGitPullRequest } from './src/data/pull-requests';

declare global {
	interface Window {
		electronApi: Readonly<{
			onAppViewUpdate: (
				listener: (pullRequests: ReadonlyArray<AdoGitPullRequest>) => void,
			) => () => void;
		}>;
	}
}

export {};
