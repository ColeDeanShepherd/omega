import { omegaPlugin } from './omega/omega-plugin';
import { IPlugin } from './plugin';

export const registeredPlugins: IPlugin[] = [
  omegaPlugin,
];