using AutoMapper;
using TaskHub.Application.Dtos.Attachments;
using TaskHub.Application.Dtos.Audit;
using TaskHub.Application.Dtos.ProjectGroups;
using TaskHub.Application.Dtos.Projects;
using TaskHub.Application.Dtos.Tasks;
using TaskHub.Application.Dtos.UserGroups;
using TaskHub.Application.Dtos.UserSettings;
using TaskHub.Domain.Entities;

namespace TaskHub.Application.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<ProjectGroup, ProjectGroupDto>()
            .ForMember(dest => dest.OwnerDisplayName, opt => opt.MapFrom(src => src.Owner != null ? src.Owner.DisplayName : string.Empty))
            .ForMember(dest => dest.ProjectCount, opt => opt.MapFrom(src => src.Projects.Count));

        CreateMap<Project, ProjectDto>()
            .ForMember(dest => dest.ProjectGroupName, opt => opt.MapFrom(src => src.ProjectGroup != null ? src.ProjectGroup.Name : null))
            .ForMember(dest => dest.OwnerDisplayName, opt => opt.MapFrom(src => src.Owner != null ? src.Owner.DisplayName : string.Empty))
            .ForMember(dest => dest.TaskCount, opt => opt.MapFrom(src => src.Tasks.Count(t => t.ParentTaskId == null)))
            .ForMember(dest => dest.SubTaskCount, opt => opt.MapFrom(src => src.Tasks.Count(t => t.ParentTaskId != null)));

        CreateMap<ProjectMember, ProjectMemberDto>()
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User != null ? src.User.UserName : string.Empty))
            .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.User != null ? src.User.DisplayName : string.Empty))
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()));

        CreateMap<TaskItem, TaskItemDto>()
            .ForMember(dest => dest.AssignedUserDisplayName, opt => opt.MapFrom(src => src.AssignedUser != null ? src.AssignedUser.DisplayName : null))
            .ForMember(dest => dest.AttachmentCount, opt => opt.MapFrom(src => src.Attachments.Count))
            .ForMember(dest => dest.SubTasks, opt => opt.MapFrom(src => src.SubTasks.OrderBy(s => s.CreatedAt)));

        CreateMap<Attachment, AttachmentDto>()
            .ForMember(dest => dest.ProjectId, opt => opt.MapFrom(src => src.TaskItem != null ? src.TaskItem.ProjectId : Guid.Empty))
            .ForMember(dest => dest.UploadedByDisplayName, opt => opt.MapFrom(src => src.UploadedBy != null ? src.UploadedBy.DisplayName : string.Empty));

        CreateMap<UserSetting, UserSettingDto>();

        // Timestamps are stored in UTC; marking the kind makes the API emit them with a "Z" suffix.
        CreateMap<UserActivityLog, UserActivityLogDto>()
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.SpecifyKind(src.CreatedAt, DateTimeKind.Utc)));

        CreateMap<UserGroup, UserGroupDto>()
            .ForMember(dest => dest.MemberCount, opt => opt.MapFrom(src => src.Members.Count));

        CreateMap<UserGroupMember, UserGroupMemberDto>()
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User != null ? src.User.UserName : string.Empty))
            .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.User != null ? src.User.DisplayName : string.Empty));
    }
}
