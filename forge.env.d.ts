/// <reference types="@electron-forge/plugin-vite/forge-vite-env" />

interface Window {
	versions: Readonly<{
		node: string;
		chrome: string;
		electron: string;
	}>;
}
