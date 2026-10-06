export type LoginFailureReason = 'UserNotFound' | 'LockedOut' | 'InvalidPassword' | 'InvalidTwoFactorCode' | 'GoogleAccountNotLinked';

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

export interface LoginLog {
  id: string;
  userId: string | null;
  userName: string;
  ipAddress: string | null;
  userAgent: string | null;
  succeeded: boolean;
  failureReason: LoginFailureReason | null;
  createdAt: string;
}

export interface UserActivityLog {
  id: string;
  userId: string | null;
  userName: string;
  action: string;
  httpMethod: string;
  path: string;
  statusCode: number;
  ipAddress: string | null;
  createdAt: string;
}
