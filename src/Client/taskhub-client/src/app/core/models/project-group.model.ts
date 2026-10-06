export interface ProjectGroup {
  id: string;
  name: string;
  description: string | null;
  ownerId: string;
  ownerDisplayName: string;
  createdAt: string;
  updatedAt: string | null;
  projectCount: number;
}

export interface CreateProjectGroupRequest {
  name: string;
  description?: string | null;
}

export interface UpdateProjectGroupRequest {
  name: string;
  description?: string | null;
}
