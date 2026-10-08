import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { TaskItem } from '../models/task.model';
import { TaskItemService } from './task-item.service';

function task(overrides: Partial<TaskItem> = {}): TaskItem {
  return {
    id: 't1',
    projectId: 'p1',
    parentTaskId: null,
    title: 'Task',
    description: 'Description',
    dueDate: '2026-05-01T00:00:00Z',
    status: 'ToDo',
    priority: 'High',
    assignedUserId: 'u1',
    assignedUserDisplayName: 'User',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: null,
    startedAt: null,
    completedAt: null,
    attachmentCount: 0,
    subTasks: [],
    ...overrides
  };
}

describe('TaskItemService', () => {
  const tasksUrl = `${environment.apiUrl}/projects/p1/tasks`;

  let service: TaskItemService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(TaskItemService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('starts a task that is moved from "to do" to "in progress"', () => {
    service.setStatus(task({ status: 'ToDo' }), 'InProgress').subscribe();

    const request = http.expectOne(`${tasksUrl}/t1/start`);
    expect(request.request.method).toBe('POST');
    request.flush(task({ status: 'InProgress' }));
  });

  it('completes a task that is moved to "done", wherever it comes from', () => {
    service.setStatus(task({ status: 'ToDo' }), 'Done').subscribe();
    http.expectOne(`${tasksUrl}/t1/complete`).flush(task({ status: 'Done' }));

    service.setStatus(task({ status: 'InProgress' }), 'Done').subscribe();
    const request = http.expectOne(`${tasksUrl}/t1/complete`);
    expect(request.request.method).toBe('POST');
    request.flush(task({ status: 'Done' }));
  });

  it('updates the task for any other move and keeps the rest of it', () => {
    service.setStatus(task({ status: 'Done', startedAt: '2026-02-01T00:00:00Z' }), 'InProgress').subscribe();

    const request = http.expectOne(`${tasksUrl}/t1`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      parentTaskId: null,
      title: 'Task',
      description: 'Description',
      dueDate: '2026-05-01T00:00:00Z',
      status: 'InProgress',
      priority: 'High',
      assignedUserId: 'u1'
    });
    request.flush(task({ status: 'InProgress' }));
  });

  it('moves a task back to "to do" with an update', () => {
    service.setStatus(task({ status: 'InProgress' }), 'ToDo').subscribe();

    const request = http.expectOne(`${tasksUrl}/t1`);
    expect(request.request.body.status).toBe('ToDo');
    request.flush(task());
  });

  it('reopens a finished task to "in progress" when it had been started, otherwise to "to do"', () => {
    service.reopen(task({ status: 'Done', startedAt: '2026-02-01T00:00:00Z' })).subscribe();
    const started = http.expectOne(`${tasksUrl}/t1`);
    expect(started.request.body.status).toBe('InProgress');
    started.flush(task());

    service.reopen(task({ status: 'Done', startedAt: null })).subscribe();
    const neverStarted = http.expectOne(`${tasksUrl}/t1`);
    expect(neverStarted.request.body.status).toBe('ToDo');
    neverStarted.flush(task());
  });

  it('asks for the tasks of every project, with the date range when one is given', () => {
    service.getAcrossProjects().subscribe();
    const all = http.expectOne(`${environment.apiUrl}/tasks`);
    expect(all.request.params.keys().length).toBe(0);
    all.flush([]);

    service.getAcrossProjects({ from: '2026-01-01', to: '2026-01-31' }).subscribe();
    const ranged = http.expectOne((request) => request.url === `${environment.apiUrl}/tasks`);
    expect(ranged.request.params.get('from')).toBe('2026-01-01');
    expect(ranged.request.params.get('to')).toBe('2026-01-31');
    ranged.flush([]);
  });
});
