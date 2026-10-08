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
  // The strongest role the user has, whether given directly or through a team.
  role: ProjectMemberRole;
  // False when the user is in the project only through a team.
  direct: boolean;
  // The teams of the project the user belongs to.
  teams: string[];
}

// A team (a user group) that has access to a project, or one that can still be added to it.
export interface ProjectTeam {
  userGroupId: string;
  name: string;
  description: string | null;
  memberCount: number;
  // Null for a team that is offered but not yet added.
  role: ProjectTeamRole | null;
}

// A whole team is never made the owner of a project.
export type ProjectTeamRole = 'Editor' | 'Viewer';

export interface AddProjectTeamRequest {
  userGroupId: string;
  role: ProjectTeamRole;
}

export interface AddProjectMemberRequest {
  userId: string;
  role: ProjectMemberRole;
}
