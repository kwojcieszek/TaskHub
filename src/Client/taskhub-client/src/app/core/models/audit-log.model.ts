export type LoginFailureReason = 'UserNotFound' | 'LockedOut' | 'InvalidPassword' | 'InvalidTwoFactorCode' | 'GoogleAccountNotLinked';

export type UserActivityType = 'Request' | 'Login';

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AuditLogQuery {
  page: number;
  pageSize: number;
  search?: string;
}

// One entry of the audit log: a login attempt or a modifying request of a signed-in user.
export interface UserActivityLog {
  id: string;
  type: UserActivityType;
  userId: string | null;
  userName: string;
  action: string;
  httpMethod: string;
  path: string;
  statusCode: number;
  ipAddress: string | null;
  // Recorded for login attempts only.
  userAgent: string | null;
  // Set on a rejected login attempt; a login without it succeeded.
  failureReason: LoginFailureReason | null;
  createdAt: string;
}
