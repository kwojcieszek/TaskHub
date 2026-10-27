using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TaskHub.Application.Common;
using TaskHub.Application.Dtos.Auth;
using TaskHub.Application.Dtos.Email;
using TaskHub.Application.Interfaces;
using TaskHub.Application.Options;
using TaskHub.Domain.Entities;
using TaskHub.Domain.Enums;

namespace TaskHub.Application.Services;

public class AuthService : IAuthService
{
    private const string AuthenticatorTokenProvider = "Authenticator";
    private const string GoogleProvider = "Google";
    private const string InvalidResetLinkMessage = "The password reset link is invalid or has expired.";

    private readonly UserManager<User> _userManager;
    private readonly IJwtTokenService _tokenService;
    private readonly IAuditLogService _auditLogService;
    private readonly ISecuritySettingsService _securitySettingsService;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _http;
    private readonly IEmailSender _emailSender;
    private readonly PasswordResetOptions _passwordResetOptions;

    public AuthService(
        UserManager<User> userManager,
        IJwtTokenService tokenService,
        IAuditLogService auditLogService,
        ISecuritySettingsService securitySettingsService,
        IGoogleTokenValidator googleTokenValidator,
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IHttpContextAccessor http,
        IEmailSender emailSender,
        IOptions<PasswordResetOptions> passwordResetOptions)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _auditLogService = auditLogService;
        _securitySettingsService = securitySettingsService;
        _googleTokenValidator = googleTokenValidator;
        _dbContext = dbContext;
        _currentUser = currentUser;
        _http = http;
        _emailSender = emailSender;
        _passwordResetOptions = passwordResetOptions.Value;
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
                TwoFactorToken = await CreateChallengeAsync(user, cancellationToken)
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

        var tokenHash = HashToken(request.TwoFactorToken);
        var now = DateTime.UtcNow;
        var challenge = await _dbContext.TwoFactorChallenges.AsNoTracking().FirstOrDefaultAsync(c => c.TokenHash == tokenHash, cancellationToken);
        if (challenge is null || challenge.UserId != user.Id || challenge.TokenVersion != user.TokenVersion ||
            challenge.SecurityStamp != user.SecurityStamp || challenge.ConsumedAt.HasValue || challenge.ExpiresAt <= now || !user.TwoFactorEnabled)
            throw new AuthenticationFailedException("Two-factor session expired, please log in again.");

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
        // Only one competing request can consume the challenge.
        var consumed = await _dbContext.TwoFactorChallenges.Where(c => c.Id == challenge.Id && c.ConsumedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(c => c.ConsumedAt, now), cancellationToken);
        if (consumed != 1) throw new AuthenticationFailedException("Two-factor challenge has already been used.");

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
        await EndOtherSessionsAsync(userId, cancellationToken);
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

    public async Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePasswordResetEnabled();

        var user = await _userManager.FindByEmailAsync(request.Email);

        // The caller gets the same answer whether or not the address belongs to an account.
        if (user?.Email is null)
        {
            return;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = $"{_passwordResetOptions.ResetUrl}?email={Uri.EscapeDataString(user.Email)}&token={encodedToken}";

        await _emailSender.SendAsync(BuildPasswordResetEmail(user, link), cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePasswordResetEnabled();

        var user = await _userManager.FindByEmailAsync(request.Email)
            ?? throw new ValidationFailedException(new[] { InvalidResetLinkMessage });

        var token = DecodeResetToken(request.Token)
            ?? throw new ValidationFailedException(new[] { InvalidResetLinkMessage });

        // A successful reset renews the security stamp, so the same link cannot be used twice.
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);

        if (!result.Succeeded)
        {
            var invalidToken = result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken));
            throw new ValidationFailedException(invalidToken ? new[] { InvalidResetLinkMessage } : result.Errors.Select(e => e.Description));
        }

        // The user picked this password, and whoever knew the old one loses every open session.
        user.MustChangePassword = false;
        user.AccessFailedCount = 0;
        user.TokenVersion++;
        await _userManager.UpdateAsync(user);
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
            PasswordResetEnabled = IsPasswordResetEnabled,
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
                TwoFactorToken = await CreateChallengeAsync(user, cancellationToken)
            };
        }

        await _auditLogService.RecordLoginAsync(user.Id, userName, null, cancellationToken);

        return await BuildLoginResponseAsync(user);
    }

    public async Task<UserDto> LinkGoogleAsync(Guid userId, GoogleTokenRequest request, CancellationToken cancellationToken = default)
    {
        var account = await GetUserOrThrowAsync(userId);
        if (!await _userManager.CheckPasswordAsync(account, request.Password ?? string.Empty) ||
            (account.TwoFactorEnabled && !await _userManager.VerifyTwoFactorTokenAsync(account, AuthenticatorTokenProvider, request.Code ?? string.Empty)))
        {
            await RegisterFailedAttemptAsync(account, cancellationToken);
            throw new ValidationFailedException(new[] { "Confirm your password and authenticator code before linking Google." });
        }
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

    private bool IsPasswordResetEnabled => _emailSender.IsConfigured && !string.IsNullOrWhiteSpace(_passwordResetOptions.ResetUrl);

    private void EnsurePasswordResetEnabled()
    {
        if (!IsPasswordResetEnabled)
        {
            throw new ForbiddenAccessException("Password reset by e-mail is not available. Ask an administrator to reset your password.");
        }
    }

    private static string? DecodeResetToken(string encodedToken)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private EmailMessage BuildPasswordResetEmail(User user, string link)
    {
        var name = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;
        var lifetime = _passwordResetOptions.TokenLifetimeMinutes;
        var encodedName = WebUtility.HtmlEncode(name);
        var encodedLink = WebUtility.HtmlEncode(link);

        return new EmailMessage
        {
            To = user.Email!,
            Subject = "TaskHub password reset",
            TextBody = $"Hello {name},\n\n" +
                $"Someone asked to reset the password of your TaskHub account. Open this link to set a new password:\n{link}\n\n" +
                $"The link is valid for {lifetime} minutes and works once. If you did not ask for it, ignore this message; your password stays unchanged.",
            HtmlBody = $"<p>Hello {encodedName},</p>" +
                "<p>Someone asked to reset the password of your TaskHub account. Open this link to set a new password:</p>" +
                $"<p><a href=\"{encodedLink}\">{encodedLink}</a></p>" +
                $"<p>The link is valid for {lifetime} minutes and works once. If you did not ask for it, ignore this message; your password stays unchanged.</p>"
        };
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
        var sessionId = Guid.NewGuid();
        var (token, expiresAt) = _tokenService.CreateAccessToken(user, roles.ToList(), sessionId);
        var now = DateTime.UtcNow;
        _dbContext.UserSessions.Add(new UserSession {
            Id = sessionId, UserId = user.Id, TokenVersion = user.TokenVersion,
            CreatedAt = now, LastUsedAt = now, ExpiresAt = expiresAt.UtcDateTime,
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent is { } agent ? agent[..Math.Min(agent.Length, 512)] : null
        });
        await _dbContext.SaveChangesAsync();

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

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task<string> CreateChallengeAsync(User user, CancellationToken ct)
    {
        var token = _tokenService.CreateTwoFactorToken(user.Id);
        await _dbContext.TwoFactorChallenges.Where(c => c.ExpiresAt < DateTime.UtcNow).ExecuteDeleteAsync(ct);
        _dbContext.TwoFactorChallenges.Add(new TwoFactorChallenge {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = HashToken(token), TokenVersion = user.TokenVersion,
            SecurityStamp = user.SecurityStamp ?? string.Empty, ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });
        await _dbContext.SaveChangesAsync(ct);
        return token;
    }

    private async Task EndOtherSessionsAsync(Guid userId, CancellationToken ct)
    {
        var currentSession = Guid.TryParse(_http.HttpContext?.User.FindFirstValue(AppClaimTypes.SessionId), out var id) ? id : Guid.Empty;
        await _dbContext.UserSessions.Where(s => s.UserId == userId && s.Id != currentSession).ExecuteDeleteAsync(ct);
        await _dbContext.TwoFactorChallenges.Where(c => c.UserId == userId).ExecuteDeleteAsync(ct);
    }

    private static string BuildAuthenticatorUri(string email, string unformattedKey)
    {
        const string issuer = "TaskHub";
        return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}?secret={unformattedKey}&issuer={Uri.EscapeDataString(issuer)}&digits=6";
    }
}
