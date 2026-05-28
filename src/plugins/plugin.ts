import { IRecurringTask } from '../recurring-task';
import { IViewInstance } from '../ui/view-instance';

export interface IPlugin {
  viewInstances?: IViewInstance[];
  recurringTasks?: IRecurringTask[];
}
