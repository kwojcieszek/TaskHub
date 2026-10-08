import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { TaskBoardMove, TaskBoardStatus } from '../../core/models/task-board.model';
import { TaskItem } from '../../core/models/task.model';
import { LanguageService } from '../../core/services/language.service';
import { TaskBoardComponent } from './task-board.component';

function task(id: string, overrides: Partial<TaskItem> = {}): TaskItem {
  return {
    id,
    projectId: 'p1',
    parentTaskId: null,
    title: `Task ${id}`,
    description: null,
    dueDate: null,
    status: 'ToDo',
    priority: 'Medium',
    assignedUserId: null,
    assignedUserDisplayName: null,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: null,
    startedAt: null,
    completedAt: null,
    attachmentCount: 0,
    subTasks: [],
    ...overrides
  };
}

function drop(item: TaskItem, from: TaskBoardStatus, to: TaskBoardStatus): CdkDragDrop<TaskBoardStatus, TaskBoardStatus, TaskItem> {
  const previousContainer = { data: from };
  const container = from === to ? previousContainer : { data: to };

  return { item: { data: item }, previousContainer, container } as unknown as CdkDragDrop<TaskBoardStatus, TaskBoardStatus, TaskItem>;
}

describe('TaskBoardComponent', () => {
  let fixture: ComponentFixture<TaskBoardComponent>;
  let component: TaskBoardComponent;

  function show(tasks: TaskItem[]): void {
    fixture.componentRef.setInput('tasks', tasks);
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TaskBoardComponent, TranslateModule.forRoot()],
      providers: [{ provide: LanguageService, useValue: { current: signal('en') } }]
    });

    fixture = TestBed.createComponent(TaskBoardComponent);
    component = fixture.componentInstance;
  });

  it('shows the three working statuses until told otherwise, in the order of the workflow', () => {
    show([]);

    expect(component.columns().map((column) => column.status)).toEqual(['ToDo', 'InProgress', 'Done']);
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.board__column').length).toBe(3);
  });

  it('shows exactly the chosen statuses, in the order of the workflow whatever order they are given in', () => {
    fixture.componentRef.setInput('statuses', ['InProgress', 'ToDo']);
    show([task('a'), task('b', { status: 'InProgress' }), task('c', { status: 'Done' })]);

    expect(component.columns().map((column) => column.status)).toEqual(['ToDo', 'InProgress']);
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.board__column').length).toBe(2);
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.board-card').length).toBe(2);
  });

  it('can show cancelled tasks in a column of their own', () => {
    fixture.componentRef.setInput('statuses', ['ToDo', 'InProgress', 'Done', 'Cancelled']);
    show([task('a'), task('d', { status: 'Cancelled' })]);

    expect(component.columns().map((column) => column.status)).toEqual(['ToDo', 'InProgress', 'Done', 'Cancelled']);
    expect(component.columns()[3].tasks.map((item) => item.id)).toEqual(['d']);
  });

  it('shows a single column when only one status is chosen', () => {
    fixture.componentRef.setInput('statuses', ['Done']);
    show([task('a'), task('c', { status: 'Done' })]);

    expect(component.columns().map((column) => column.status)).toEqual(['Done']);
    expect((fixture.nativeElement as HTMLElement).querySelector('.board')?.getAttribute('style')).toContain('--board-columns: 1');
  });

  it('puts every task into the column of its status and leaves cancelled ones out by default', () => {
    show([
      task('a'),
      task('b', { status: 'InProgress' }),
      task('c', { status: 'Done' }),
      task('d', { status: 'Cancelled' }),
      task('e')
    ]);

    const ids = component.columns().map((column) => column.tasks.map((item) => item.id));

    expect(ids).toEqual([['a', 'e'], ['b'], ['c']]);
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.board-card').length).toBe(4);
  });

  it('orders open tasks by due date, with the ones without a date last', () => {
    show([
      task('none'),
      task('late', { dueDate: '2026-06-01T00:00:00Z' }),
      task('early', { dueDate: '2026-02-01T00:00:00Z' })
    ]);

    expect(component.columns()[0].tasks.map((item) => item.id)).toEqual(['early', 'late', 'none']);
  });

  it('orders finished tasks with the most recently completed first', () => {
    show([
      task('old', { status: 'Done', completedAt: '2026-01-10T00:00:00Z' }),
      task('new', { status: 'Done', completedAt: '2026-03-10T00:00:00Z' })
    ]);

    expect(component.columns()[2].tasks.map((item) => item.id)).toEqual(['new', 'old']);
  });

  it('shows only the first cards of a very long column and counts the rest', () => {
    show(Array.from({ length: 65 }, (_, index) => task(`t${index}`)));

    const column = component.columns()[0];

    expect(column.tasks.length).toBe(60);
    expect(column.hidden).toBe(5);
  });

  it('reports a card dropped into another column', () => {
    const moves: TaskBoardMove[] = [];
    const moved = task('a');
    component.move.subscribe((move) => moves.push(move));

    component.onDrop(drop(moved, 'ToDo', 'Done'));

    expect(moves).toEqual([{ task: moved, status: 'Done' }]);
  });

  it('reports nothing for a card dropped back into its own column', () => {
    const moves: TaskBoardMove[] = [];
    component.move.subscribe((move) => moves.push(move));

    component.onDrop(drop(task('a'), 'ToDo', 'ToDo'));

    expect(moves).toEqual([]);
  });

  it('locks tasks whose status follows their subtasks', () => {
    fixture.componentRef.setInput('lockedTaskIds', new Set(['parent']));
    show([task('parent'), task('free')]);

    const cards = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.board-card'));

    expect(component.isLocked(task('parent'))).toBeTrue();
    expect(component.isLocked(task('free'))).toBeFalse();
    expect(cards.filter((card) => card.classList.contains('board-card--locked')).length).toBe(1);
  });

  it('marks an open task as overdue once its due date has passed', () => {
    expect(component.isOverdue(task('a', { dueDate: '2000-01-01T00:00:00Z' }))).toBeTrue();
    expect(component.isOverdue(task('b', { dueDate: '2999-01-01T00:00:00Z' }))).toBeFalse();
    expect(component.isOverdue(task('c', { dueDate: '2000-01-01T00:00:00Z', status: 'Done' }))).toBeFalse();
    expect(component.isOverdue(task('d'))).toBeFalse();
    expect(component.isOverdue(task('e', { dueDate: '2000-01-01T00:00:00Z', status: 'Cancelled' }))).toBeFalse();
  });

  it('opens a task when its card is clicked', () => {
    const opened: TaskItem[] = [];
    component.open.subscribe((item) => opened.push(item));
    show([task('a')]);

    ((fixture.nativeElement as HTMLElement).querySelector('.board-card') as HTMLElement).click();

    expect(opened.map((item) => item.id)).toEqual(['a']);
  });

  it('shows the name of the project and the initial of the assignee', () => {
    fixture.componentRef.setInput('projectNames', { p1: 'Website' });
    show([task('a', { assignedUserDisplayName: 'anna' })]);

    const card = (fixture.nativeElement as HTMLElement).querySelector('.board-card') as HTMLElement;

    expect(card.querySelector('.board-card__project')?.textContent).toContain('Website');
    expect(card.querySelector('.board-card__assignee')?.textContent?.trim()).toBe('A');
    expect(component.projectName(task('x', { projectId: 'unknown' }))).toBe('');
  });
});
