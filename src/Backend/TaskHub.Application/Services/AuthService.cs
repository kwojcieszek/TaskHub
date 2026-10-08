using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Interfaces;
using TaskHub.Domain.Entities;
using TaskHub.Domain.Enums;

namespace TaskHub.Application.Services;

public class AuthService : IAuthService
{
    private const string AuthenticatorTokenProvider = "Authenticator";
    private const string GoogleProvider = "Google";

    private readonly UserManager<User> _userManager;
    private readonly IJwtTokenService _tokenService;
    private readonly IAuditLogService _auditLogService;
    private readonly ISecuritySettingsService _securitySettingsService;
    private readonly IGoogleTokenValidator _googleTokenValidator;

    public AuthService(
        UserManager<User> userManager,
        IJwtTokenService tokenService,
        IAuditLogService auditLogService,
        ISecuritySettingsService securitySettingsService,
        IGoogleTokenValidator googleTokenValidator)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _auditLogService = auditLogService;
        _securitySettingsService = securitySettingsService;
        _googleTokenValidator = googleTokenValidator;
    }

    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await _securitySettingsService.GetAsync(cancellationToken);

        if (!settings.RegistrationEnabled)
        {
            throw new ForbiddenAccessException("Registration is turned off. Ask an administrator for an account.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = request.UserName,
            Email = request.Email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.UserName : request.DisplayName
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            throw new ValidationFailedException(result.Errors.Select(e => e.Description));
        }

        await _userManager.AddToRoleAsync(user, Roles.User);

        return await MapToUserDtoAsync(user);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByNameAsync(request.UserName);

        if (user is null)
        {
            await _auditLogService.RecordLoginAsync(null, request.UserName ?? string.Empty, LoginFailureReason.UserNotFound, cancellationToken);
            throw new AuthenticationFailedException("Invalid username or password.");
        }

        var userName = user.UserName ?? string.Empty;

        if (await _userManager.IsLockedOutAsync(user))
        {
            await _auditLogService.RecordLoginAsync(user.Id, userName, LoginFailureReason.LockedOut, cancellationToken);
            throw new AuthenticationFailedException("This account is locked out.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);

        if (!passwordValid)
        {
            await RegisterFailedAttemptAsync(user, cancellationToken);
            await _auditLogService.RecordLoginAsync(user.Id, userName, LoginFailureReason.InvalidPassword, cancellationToken);
            throw new AuthenticationFailedException("Invalid username or password.");
        }

        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return new LoginResponse
            {
                RequiresTwoFactor = true,
                TwoFactorToken = _tokenService.CreateTwoFactorToken(user.Id)
            };
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        await _auditLogService.RecordLoginAsync(user.Id, userName, null, cancellationToken);

        return await BuildLoginResponseAsync(user);
    }

    public async Task<LoginResponse> VerifyTwoFactorAsync(TwoFactorVerifyRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _tokenService.ValidateTwoFactorToken(request.TwoFactorToken)
            ?? throw new AuthenticationFailedException("Two-factor session expired, please log in again.");

        var user = await _userManager.FindByIdAsync(userId.ToString()!)
            ?? throw new AuthenticationFailedException("Invalid session.");

        var userName = user.UserName ?? string.Empty;

        if (await _userManager.IsLockedOutAsync(user))
        {
            await _auditLogService.RecordLoginAsync(user.Id, userName, LoginFailureReason.LockedOut, cancellationToken);
            throw new AuthenticationFailedException("This account is locked out.");
        }

        var codeValid = await _userManager.VerifyTwoFactorTokenAsync(user, AuthenticatorTokenProvider, request.Code);

        if (!codeValid)
        {
            await RegisterFailedAttemptAsync(user, cancellationToken);
            await _auditLogService.RecordLoginAsync(user.Id, userName, LoginFailureReason.InvalidTwoFactorCode, cancellationToken);
            throw new AuthenticationFailedException("Invalid authenticator code.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        await _auditLogService.RecordLoginAsync(user.Id, userName, null, cancellationToken);

        return await BuildLoginResponseAsync(user);
    }

    public async Task<TwoFactorSetupResponse> BeginTwoFactorSetupAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);

        // Starting a new setup replaces the authenticator key, so it must not be possible while 2FA protects the account.
        if (user.TwoFactorEnabled)
        {
            throw new ConflictException("Two-factor authentication is already enabled. Disable it first to set it up again.");
        }

        await _userManager.ResetAuthenticatorKeyAsync(user);
        var key = await _userManager.GetAuthenticatorKeyAsync(user)
            ?? throw new InvalidOperationException("Unable to generate authenticator key.");

        var uri = BuildAuthenticatorUri(user.Email ?? user.UserName ?? "user", key);

        return new TwoFactorSetupResponse
        {
            SharedKey = key,
            AuthenticatorUri = uri
        };
    }

    public async Task EnableTwoFactorAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);

        var codeValid = await _userManager.VerifyTwoFactorTokenAsync(user, AuthenticatorTokenProvider, code);

        if (!codeValid)
        {
            throw new ValidationFailedException(new[] { "Invalid authenticator code." });
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
    }

    public async Task DisableTwoFactorAsync(Guid userId, TwoFactorDisableRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);

        // A signed-in session alone is not enough to remove the second factor.
        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new ValidationFailedException(new[] { "Invalid password." });
        }

        await _userManager.SetTwoFactorEnabledAsync(user, false);
    }

    public async Task<LoginResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);

        if (request.NewPassword == request.CurrentPassword)
        {
            throw new ValidationFailedException(new[] { "The new password must differ from the current one." });
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            throw new ValidationFailedException(result.Errors.Select(e => e.Description));
        }

        // Every other session of the user ends here; the caller continues with the token returned below.
        user.MustChangePassword = false;
        user.TokenVersion++;
        await _userManager.UpdateAsync(user);

        return await BuildLoginResponseAsync(user);
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        return await MapToUserDtoAsync(user);
    }

    public async Task<AuthProvidersDto> GetProvidersAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _securitySettingsService.GetAsync(cancellationToken);

        return new AuthProvidersDto
        {
            GoogleClientId = _googleTokenValidator.ClientId,
            RegistrationEnabled = settings.RegistrationEnabled,
            PasswordPolicy = settings.PasswordPolicy
        };
    }

    public async Task<LoginResponse> LoginWithGoogleAsync(GoogleTokenRequest request, CancellationToken cancellationToken = default)
    {
        var googleUser = await _googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken)
            ?? throw new AuthenticationFailedException("Google sign-in failed.");

        // Only an account linked beforehand from the profile can sign in; a matching e-mail alone is not enough.
        var user = await _userManager.FindByLoginAsync(GoogleProvider, googleUser.Subject);

        if (user is null)
        {
            await _auditLogService.RecordLoginAsync(null, googleUser.Email, LoginFailureReason.GoogleAccountNotLinked, cancellationToken);
            throw new AuthenticationFailedException("This Google account is not linked to any user.");
        }

        var userName = user.UserName ?? string.Empty;

        if (await _userManager.IsLockedOutAsync(user))
        {
            await _auditLogService.RecordLoginAsync(user.Id, userName, LoginFailureReason.LockedOut, cancellationToken);
            throw new AuthenticationFailedException("This account is locked out.");
        }

        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return new LoginResponse
            {
                RequiresTwoFactor = true,
                TwoFactorToken = _tokenService.CreateTwoFactorToken(user.Id)
            };
        }

        await _auditLogService.RecordLoginAsync(user.Id, userName, null, cancellationToken);

        return await BuildLoginResponseAsync(user);
    }

    public async Task<UserDto> LinkGoogleAsync(Guid userId, GoogleTokenRequest request, CancellationToken cancellationToken = default)
    {
        var googleUser = await _googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken)
            ?? throw new ValidationFailedException(new[] { "The Google account could not be verified." });

        if (!googleUser.EmailVerified)
        {
            throw new ValidationFailedException(new[] { "The Google account has no verified e-mail address." });
        }

        var user = await GetUserOrThrowAsync(userId);

        if (await FindGoogleLoginAsync(user) is not null)
        {
            throw new ConflictException("A Google account is already linked to this user.");
        }

        var owner = await _userManager.FindByLoginAsync(GoogleProvider, googleUser.Subject);

        if (owner is not null)
        {
            throw new ConflictException("This Google account is already linked to another user.");
        }

        var result = await _userManager.AddLoginAsync(user, new UserLoginInfo(GoogleProvider, googleUser.Subject, googleUser.Email));

        if (!result.Succeeded)
        {
            throw new ValidationFailedException(result.Errors.Select(e => e.Description));
        }

        return await MapToUserDtoAsync(user);
    }

    public async Task<UserDto> UnlinkGoogleAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);
        var login = await FindGoogleLoginAsync(user);

        if (login is not null)
        {
            var result = await _userManager.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);

            if (!result.Succeeded)
            {
                throw new ValidationFailedException(result.Errors.Select(e => e.Description));
            }
        }

        return await MapToUserDtoAsync(user);
    }

    private async Task<UserLoginInfo?> FindGoogleLoginAsync(User user)
    {
        var logins = await _userManager.GetLoginsAsync(user);

        return logins.FirstOrDefault(l => l.LoginProvider == GoogleProvider);
    }

    private async Task RegisterFailedAttemptAsync(User user, CancellationToken cancellationToken)
    {
        var settings = await _securitySettingsService.GetAsync(cancellationToken);

        if (!settings.LockoutEnabled)
        {
            return;
        }

        user.AccessFailedCount++;

        if (user.AccessFailedCount >= settings.MaxFailedAccessAttempts)
        {
            user.AccessFailedCount = 0;
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(settings.LockoutMinutes);
        }

        await _userManager.UpdateAsync(user);
    }

    private async Task<LoginResponse> BuildLoginResponseAsync(User user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.CreateAccessToken(user, roles.ToList());

        return new LoginResponse
        {
            RequiresTwoFactor = false,
            AccessToken = token,
            ExpiresAt = expiresAt,
            User = await MapToUserDtoAsync(user)
        };
    }

    private async Task<UserDto> MapToUserDtoAsync(User user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var googleLogin = await FindGoogleLoginAsync(user);

        return new UserDto
        {
            GoogleLinked = googleLogin is not null,
            GoogleEmail = googleLogin?.ProviderDisplayName,
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            TwoFactorEnabled = user.TwoFactorEnabled,
            MustChangePassword = user.MustChangePassword,
            Roles = roles.ToList()
        };
    }

    private async Task<User> GetUserOrThrowAsync(Guid userId)
    {
        return await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("User not found.");
    }

    private static string BuildAuthenticatorUri(string email, string unformattedKey)
    {
        const string issuer = "TaskHub";
        return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}?secret={unformattedKey}&issuer={Uri.EscapeDataString(issuer)}&digits=6";
    }
}
