// Preload script for the Electron application.

import { contextBridge, ipcRenderer } from 'electron';
import type { AdoGitPullRequest } from './data/pull-requests';

type AppViewListener = (pullRequests: ReadonlyArray<AdoGitPullRequest>) => void;

const electronApi = Object.freeze({
	onAppViewUpdate: (listener: AppViewListener) => {
		const subscription = (
			_event: Electron.IpcRendererEvent,
			pullRequests: ReadonlyArray<AdoGitPullRequest>,
		) => {
			listener(pullRequests);
		};

		ipcRenderer.on('app-view:update', subscription);

		return () => {
			ipcRenderer.removeListener('app-view:update', subscription);
		};
	},
});

contextBridge.exposeInMainWorld('electronApi', electronApi);
