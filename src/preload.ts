// Preload script for the Electron application.

import { contextBridge, ipcRenderer } from 'electron';

import type { AdoGitPullRequest } from './plugins/omega/pull-requests';

const electronApi = Object.freeze({
	loadPullRequests: (
		organization: string,
		projects: ReadonlyArray<string>,
	): Promise<ReadonlyArray<AdoGitPullRequest>> =>
		ipcRenderer.invoke('ado:loadPullRequests', organization, projects) as Promise<ReadonlyArray<AdoGitPullRequest>>,
});

contextBridge.exposeInMainWorld('electronApi', electronApi);
