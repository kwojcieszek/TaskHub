export interface AdminUser {
  id: string;
  userName: string;
  email: string;
  displayName: string;
  lockedOut: boolean;
  twoFactorEnabled: boolean;
  lastLoginAt: string | null;
  roles: string[];
}

export interface CreateUserRequest {
  userName: string;
  email: string;
  displayName: string;
  password: string;
}

export interface UpdateUserRolesRequest {
  roles: string[];
}

export interface SetUserLockoutRequest {
  lockedOut: boolean;
}
