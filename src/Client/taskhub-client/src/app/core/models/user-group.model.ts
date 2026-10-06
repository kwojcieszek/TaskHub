export interface UserGroup {
  id: string;
  name: string;
  description: string | null;
  memberCount: number;
}

export interface CreateUserGroupRequest {
  name: string;
  description?: string | null;
}

export interface UserGroupMember {
  userId: string;
  userName: string;
  displayName: string;
}

export interface AddUserGroupMemberRequest {
  userId: string;
}
