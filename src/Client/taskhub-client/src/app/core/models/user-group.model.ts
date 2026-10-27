export interface UserGroup {
  id: string;
  name: string;
  description: string | null;
  memberCount: number;
  canManage: boolean;
  ownerId: string | null;
  ownerDisplayName: string | null;
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

export interface UpdateUserGroupRequest {
  name: string;
  description?: string | null;
}

export interface AddUserGroupMemberRequest {
  userId: string;
}
