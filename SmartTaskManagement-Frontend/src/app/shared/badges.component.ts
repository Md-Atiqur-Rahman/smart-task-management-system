import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TaskItemStatus, TaskPriority } from '../core/models/api.models';
import { priorityLabel, statusLabel } from './labels';

@Component({
  selector: 'app-status-badge',
  template: `<span class="badge status-{{ status() }}">{{ label() }}</span>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusBadgeComponent {
  readonly status = input.required<TaskItemStatus>();
  protected readonly label = computed(() => statusLabel(this.status()));
}

@Component({
  selector: 'app-priority-badge',
  template: `<span class="badge priority-{{ priority() }}">{{ label() }}</span>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PriorityBadgeComponent {
  readonly priority = input.required<TaskPriority>();
  protected readonly label = computed(() => priorityLabel(this.priority()));
}
