import { RRule } from 'rrule';
import { pickMinimum } from './array-utils';

export interface IRecurringTask {
  recurrenceRules: ReadonlyArray<RRule>;
  runFn: () => void | Promise<void>;
}

const getNextRuleOccurrence = (rule: RRule, now: Date): Date | null =>
  rule.after(now, /* inc: */ true);

const getNextOccurrence = (rules: ReadonlyArray<RRule>, now: Date): Date | null =>
  pickMinimum(
    /* items: */
    rules
      .map((rule) => getNextRuleOccurrence(rule, now))
      .filter((date): date is Date => date !== null),
    /* getValue: */
    (date) => date.getTime(),
  );

export class RecurringTaskRunner {
  private timeout: ReturnType<typeof setTimeout> | null = null;
  private running = false;

  constructor(
    private task: IRecurringTask) {
  }

  start(): void {
    if (this.running) {
      return;
    }

    this.running = true;
    this.scheduleNext();
  }

  stop(): void {
    this.running = false;

    if (this.timeout) {
      clearTimeout(this.timeout);
      this.timeout = null;
    }
  }

  getNextRunAt(): Date | null {
    return getNextOccurrence(this.task.recurrenceRules, new Date());
  }

  private scheduleNext(): void {
    if (!this.running) {
      return;
    }

    const nextRun = this.getNextRunAt();
    if (!nextRun) {
      return;
    }

    const delayMs = Math.max(0, nextRun.getTime() - Date.now());
    this.timeout = setTimeout(() => {
      void this.runAndReschedule();
    }, delayMs);
  }

  private async runAndReschedule(): Promise<void> {
    if (!this.running) {
      return;
    }

    try {
      await this.task.runFn();
    } catch (error) {
      console.error('Recurring task failed.', error);
    } finally {
      this.scheduleNext();
    }
  }
}
