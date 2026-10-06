export interface Project {
  id: string;
  name: string;
  description: string | null;
  icon: string | null;
  gitUrl: string | null;
  isActive: boolean;
  projectGroupId: string | null;
  projectGroupName: string | null;
  ownerId: string;
  ownerDisplayName: string;
  createdAt: string;
  updatedAt: string | null;
  taskCount: number;
  subTaskCount: number;
}

export interface CreateProjectRequest {
  name: string;
  description?: string | null;
  icon?: string | null;
  gitUrl?: string | null;
  projectGroupId?: string | null;
}

export interface UpdateProjectRequest {
  name: string;
  description?: string | null;
  icon?: string | null;
  gitUrl?: string | null;
  isActive: boolean;
}

export type ProjectMemberRole = 'Owner' | 'Editor' | 'Viewer';

export interface ProjectMember {
  userId: string;
  userName: string;
  displayName: string;
  role: ProjectMemberRole;
}

export interface AddProjectMemberRequest {
  userId: string;
  role: ProjectMemberRole;
}
